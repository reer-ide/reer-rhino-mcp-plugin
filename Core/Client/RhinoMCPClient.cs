using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Functions;

namespace ReerRhinoMCPPlugin.Core.Client
{
    /// <summary>
    /// Remote WebSocket client implementation for MCP connections
    /// </summary>
    public class RhinoMCPClient : IRhinoMCPConnection
    {
        private readonly object lockObject = new object();
        private readonly CommandExecutor commandHandler;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly HttpClient httpClient;
        private readonly LicenseManager licenseManager;
        private readonly FileIntegrityManager fileIntegrityManager;
        
        private ClientWebSocket webSocket;
        private Task receiveTask;
        private bool disposed;
        private ConnectionStatus status = ConnectionStatus.Disconnected;
        private string sessionId;
        private string instanceId;
        private string licenseId;
        private string currentFilePath;
        private string currentFileHash;

        public RhinoMCPClient()
        {
            commandHandler = new CommandExecutor();
            cancellationTokenSource = new CancellationTokenSource();
            httpClient = new HttpClient();
            licenseManager = new LicenseManager();
            fileIntegrityManager = new FileIntegrityManager();
        }
        
        /// <summary>
        /// Indicates whether the connection is currently active
        /// </summary>
        public bool IsConnected => status == ConnectionStatus.Connected;
        
        /// <summary>
        /// The current connection status
        /// </summary>
        public ConnectionStatus Status => status;
        
        /// <summary>
        /// The connection settings being used
        /// </summary>
        public ConnectionSettings Settings { get; private set; }
        
        /// <summary>
        /// Event fired when a command is received from a client
        /// </summary>
#pragma warning disable CS0067 // Event is never used - will be implemented when WebSocket client is added
        public event EventHandler<CommandReceivedEventArgs> CommandReceived;
#pragma warning restore CS0067
        
        /// <summary>
        /// Event fired when the connection status changes
        /// </summary>
        public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;
        
        /// <summary>
        /// Register a license with the remote server (one-time initialization)
        /// </summary>
        /// <param name="licenseKey">License key provided by the host app</param>
        /// <param name="userId">User identifier</param>
        /// <param name="serverUrl">Remote MCP server URL</param>
        /// <returns>True if registration successful, false otherwise</returns>
        public async Task<bool> RegisterLicenseAsync(string licenseKey, string userId, string serverUrl)
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP License Registration ===");
                
                var result = await licenseManager.RegisterLicenseAsync(licenseKey, userId, serverUrl);
                
                if (result.Success)
                {
                    RhinoApp.WriteLine($"✓ License registration successful!");
                    RhinoApp.WriteLine($"  License ID: {result.LicenseId}");
                    RhinoApp.WriteLine($"  Tier: {result.Tier}");
                    RhinoApp.WriteLine($"  Max concurrent files: {result.MaxConcurrentFiles}");
                    RhinoApp.WriteLine("  License stored securely for future use");
                    return true;
                }
                else
                {
                    RhinoApp.WriteLine($"✗ License registration failed: {result.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"License registration error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get the stored license status
        /// </summary>
        /// <returns>License validation result</returns>
        public async Task<LicenseValidationResult> GetLicenseStatusAsync()
        {
            return await licenseManager.ValidateLicenseAsync();
        }
        
        /// <summary>
        /// Clear stored license (for troubleshooting)
        /// </summary>
        public void ClearStoredLicense()
        {
            licenseManager.ClearStoredLicense();
            RhinoApp.WriteLine("Stored license cleared. You will need to register again.");
        }
        
        /// <summary>
        /// Validate all linked files and report any issues
        /// </summary>
        /// <returns>List of file status changes</returns>
        public async Task<List<FileStatusChange>> ValidateLinkedFilesAsync()
        {
            return await fileIntegrityManager.ValidateLinkedFilesAsync();
        }
        
        /// <summary>
        /// Check if file is valid for reconnection
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="expectedFilePath">Expected file path</param>
        /// <param name="expectedHash">Expected file hash</param>
        /// <returns>File validation result</returns>
        public async Task<FileValidationResult> ValidateFileForReconnectionAsync(string sessionId, string expectedFilePath, string expectedHash)
        {
            return await fileIntegrityManager.ValidateFileForReconnectionAsync(sessionId, expectedFilePath, expectedHash);
        }
        
        /// <summary>
        /// Get all linked files for status reporting
        /// </summary>
        /// <returns>List of linked file information</returns>
        public List<LinkedFileInfo> GetLinkedFiles()
        {
            return fileIntegrityManager.GetAllLinkedFiles();
        }
        
        /// <summary>
        /// Clear all linked files (for troubleshooting)
        /// </summary>
        public async Task ClearLinkedFilesAsync()
        {
            await fileIntegrityManager.ClearAllLinkedFilesAsync();
            RhinoApp.WriteLine("All linked files cleared.");
        }
        
        /// <summary>
        /// Send file status updates to the remote server
        /// </summary>
        /// <param name="statusChanges">List of status changes to report</param>
        public async Task ReportFileStatusChangesAsync(List<FileStatusChange> statusChanges)
        {
            if (!statusChanges.Any() || status != ConnectionStatus.Connected)
                return;
                
            try
            {
                var notification = new
                {
                    type = "file_status_update",
                    license_id = licenseId,
                    status_changes = statusChanges.Select(sc => new
                    {
                        session_id = sc.SessionId,
                        file_path = sc.FilePath,
                        old_status = sc.OldStatus.ToString(),
                        new_status = sc.NewStatus.ToString(),
                        message = sc.Message,
                        timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    })
                };
                
                await SendResponseAsync(JsonConvert.SerializeObject(notification));
                
                RhinoApp.WriteLine($"Reported {statusChanges.Count} file status changes to server");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error reporting file status changes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Starts the connection with the specified settings
        /// </summary>
        /// <param name="settings">Connection settings to use</param>
        /// <returns>True if connection started successfully, false otherwise</returns>
        public Task<bool> StartAsync(ConnectionSettings settings)
        {
            if (settings == null || settings.Mode != ConnectionMode.Remote)
                return false;
                
            if (disposed)
                throw new ObjectDisposedException(nameof(RhinoMCPClient));

            lock (lockObject)
            {
                if (status != ConnectionStatus.Disconnected)
                {
                    RhinoApp.WriteLine("WebSocket client is already running or starting");
                    return false;
                }

                status = ConnectionStatus.Connecting;
                Settings = settings;
            }

            OnStatusChanged(ConnectionStatus.Connecting, "Validating license and creating session...");

            try
            {
                // Step 1: Validate license
                var licenseValidation = await licenseManager.ValidateLicenseAsync();
                if (!licenseValidation.IsValid)
                {
                    throw new Exception($"License validation failed: {licenseValidation.Message}");
                }
                
                licenseId = licenseValidation.LicenseId;
                RhinoApp.WriteLine($"✓ License validated: {licenseId} (Tier: {licenseValidation.Tier})");

                // Step 2: Create session with remote server
                string websocketUrl = await CreateSessionAsync(licenseValidation);
                if (string.IsNullOrEmpty(websocketUrl))
                {
                    throw new Exception("Failed to create session or get WebSocket URL");
                }

                // Step 3: Connect to WebSocket
                webSocket = new ClientWebSocket();
                
                var uri = new Uri(websocketUrl);
                await webSocket.ConnectAsync(uri, cancellationTokenSource.Token);

                lock (lockObject)
                {
                    status = ConnectionStatus.Connected;
                }

                // Step 4: Start receiving messages
                receiveTask = Task.Run(() => ReceiveMessagesAsync(cancellationTokenSource.Token));

                OnStatusChanged(ConnectionStatus.Connected, $"WebSocket client connected to {websocketUrl}");
                RhinoApp.WriteLine($"✓ RhinoMCP WebSocket client connected successfully");

                return true;
            }
            catch (Exception ex)
            {
                lock (lockObject)
                {
                    status = ConnectionStatus.Failed;
                }

                OnStatusChanged(ConnectionStatus.Failed, $"Failed to connect WebSocket client: {ex.Message}", ex);
                RhinoApp.WriteLine($"✗ Failed to connect RhinoMCP WebSocket client: {ex.Message}");

                await CleanupAsync();
                return false;
            }
        }
        
        /// <summary>
        /// Stops the connection and cleans up resources
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        public Task StopAsync()
        {
            ConnectionStatus currentStatus;
            lock (lockObject)
            {
                currentStatus = status;
                if (currentStatus == ConnectionStatus.Disconnected)
                    return;

                status = ConnectionStatus.Disconnected;
            }

            OnStatusChanged(ConnectionStatus.Disconnected, "Stopping WebSocket client...");
            RhinoApp.WriteLine("Stopping RhinoMCP WebSocket client...");

            // Unregister file if we have a session
            if (!string.IsNullOrEmpty(sessionId))
            {
                await fileIntegrityManager.UnregisterLinkedFileAsync(sessionId);
            }

            await CleanupAsync();

            OnStatusChanged(ConnectionStatus.Disconnected, "WebSocket client stopped");
            RhinoApp.WriteLine("RhinoMCP WebSocket client stopped");
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Sends a response back to the client
        /// </summary>
        /// <param name="responseJson">JSON response to send</param>
        /// <param name="clientId">ID of the client to send to (for multi-client scenarios)</param>
        /// <returns>True if response was sent successfully, false otherwise</returns>
        public async Task<bool> SendResponseAsync(string responseJson, string clientId = null)
        {
            if (string.IsNullOrEmpty(responseJson) || webSocket?.State != WebSocketState.Open)
                return false;

            try
            {
                var buffer = Encoding.UTF8.GetBytes(responseJson);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    cancellationTokenSource.Token);
                
                return true;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error sending WebSocket response: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a session with the remote server using license-based authentication
        /// </summary>
        private async Task<string> CreateSessionAsync(LicenseValidationResult licenseInfo)
        {
            try
            {
                // Get current file path
                currentFilePath = GetCurrentRhinoFilePath();
                
                // Calculate file hash for integrity validation
                currentFileHash = await fileIntegrityManager.CalculateFileHashAsync(currentFilePath);
                var fileSize = fileIntegrityManager.GetFileSize(currentFilePath);
                
                RhinoApp.WriteLine($"✓ File hash calculated: {currentFileHash?.Substring(0, 16)}...");
                
                // Create session request with license authentication and file info
                var sessionRequest = new
                {
                    user_id = licenseInfo.UserId,
                    file_path = currentFilePath,
                    file_hash = currentFileHash,
                    file_size = fileSize,
                    license_id = licenseInfo.LicenseId
                };

                var jsonContent = JsonConvert.SerializeObject(sessionRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{Settings.RemoteUrl}/sessions/create", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Session creation failed: {response.StatusCode} - {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var sessionData = JsonConvert.DeserializeObject<JObject>(responseJson);

                sessionId = sessionData["session_id"]?.ToString();
                instanceId = sessionData["instance_id"]?.ToString();
                string websocketUrl = sessionData["websocket_url"]?.ToString();

                // Register file with integrity manager
                await fileIntegrityManager.RegisterLinkedFileAsync(sessionId, currentFilePath, currentFileHash);

                RhinoApp.WriteLine($"✓ Session created - ID: {sessionId}");
                RhinoApp.WriteLine($"  File: {Path.GetFileName(currentFilePath)}");
                RhinoApp.WriteLine($"  File size: {fileSize:N0} bytes");
                
                return websocketUrl;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error creating session: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Get the current Rhino file path
        /// </summary>
        private string GetCurrentRhinoFilePath()
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc != null && !string.IsNullOrEmpty(doc.Path))
                {
                    return doc.Path;
                }
                else
                {
                    // Return a placeholder for unsaved documents
                    return $"/rhino/unsaved_document_{DateTime.Now:yyyyMMdd_HHmmss}.3dm";
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Warning: Could not get current file path: {ex.Message}");
                return $"/rhino/unknown_document_{DateTime.Now:yyyyMMdd_HHmmss}.3dm";
            }
        }

        /// <summary>
        /// Continuously receives messages from the WebSocket
        /// </summary>
        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var messageBuilder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && webSocket?.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageChunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        messageBuilder.Append(messageChunk);

                        if (result.EndOfMessage)
                        {
                            var message = messageBuilder.ToString();
                            messageBuilder.Clear();
                            
                            // Process the complete message
                            await ProcessIncomingMessageAsync(message);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        RhinoApp.WriteLine("WebSocket connection closed by server");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error receiving WebSocket messages: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes incoming messages from the WebSocket
        /// </summary>
        private async Task ProcessIncomingMessageAsync(string message)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<JObject>(message);
                var messageType = data["type"]?.ToString();

                RhinoApp.WriteLine($"Received message type: {messageType}");

                switch (messageType)
                {
                    case "handshake":
                        await HandleHandshakeAsync(data);
                        break;
                        
                    case "command":
                        await HandleCommandAsync(data);
                        break;
                        
                    case "heartbeat":
                        await HandleHeartbeatAsync(data);
                        break;
                        
                    default:
                        RhinoApp.WriteLine($"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error processing incoming message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles handshake messages from the server
        /// </summary>
        private async Task HandleHandshakeAsync(JObject data)
        {
            sessionId = data["session_id"]?.ToString();
            instanceId = data["instance_id"]?.ToString();
            var filePath = data["file_path"]?.ToString();
            
            RhinoApp.WriteLine($"✓ Handshake completed");
            RhinoApp.WriteLine($"  Session: {sessionId}");
            RhinoApp.WriteLine($"  Instance: {instanceId}");
            RhinoApp.WriteLine($"  File: {filePath}");
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles command messages from the server
        /// </summary>
        private async Task HandleCommandAsync(JObject data)
        {
            try
            {
                var tool = data["tool"]?.ToString();
                var parameters = data["params"] as JObject ?? new JObject();
                var correlationId = data["correlation_id"]?.ToString();

                RhinoApp.WriteLine($"Processing command: {tool} with correlation ID: {correlationId}");

                // Fire command received event
                var commandArgs = new CommandReceivedEventArgs(data.ToString(), data, instanceId ?? "unknown");
                CommandReceived?.Invoke(this, commandArgs);

                // Process command using the command handler
                string result = await Task.Run(() => commandHandler.ProcessCommand(new JObject
                {
                    ["type"] = tool,
                    ["params"] = parameters
                }, instanceId ?? "unknown"));

                // Send response back with correlation ID
                var response = new JObject
                {
                    ["type"] = "response",
                    ["correlation_id"] = correlationId,
                    ["result"] = JObject.Parse(result)
                };

                await SendResponseAsync(response.ToString());
                RhinoApp.WriteLine($"Response sent for correlation_id: {correlationId}");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error handling command: {ex.Message}");
                
                // Send error response
                var errorResponse = new JObject
                {
                    ["type"] = "response",
                    ["correlation_id"] = data["correlation_id"]?.ToString(),
                    ["status"] = "error",
                    ["message"] = ex.Message
                };

                await SendResponseAsync(errorResponse.ToString());
            }
        }

        /// <summary>
        /// Handles heartbeat messages from the server
        /// </summary>
        private async Task HandleHeartbeatAsync(JObject data)
        {
            var heartbeatResponse = new JObject
            {
                ["type"] = "heartbeat_ack",
                ["session_id"] = sessionId,
                ["timestamp"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            await SendResponseAsync(heartbeatResponse.ToString());
            // RhinoApp.WriteLine("Heartbeat response sent"); // Comment out to reduce noise
        }

        /// <summary>
        /// Fires the status changed event
        /// </summary>
        private void OnStatusChanged(ConnectionStatus newStatus, string message = null, Exception exception = null)
        {
            try
            {
                StatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(newStatus, message, exception));
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error in status changed handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up resources
        /// </summary>
        private async Task CleanupAsync()
        {
            try
            {
                // Cancel operations
                cancellationTokenSource.Cancel();

                // Close WebSocket connection
                if (webSocket?.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }

                // Wait for receive task to complete
                if (receiveTask != null)
                {
                    await receiveTask.ConfigureAwait(false);
                }

                // Dispose WebSocket
                webSocket?.Dispose();
                webSocket = null;
                receiveTask = null;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes the client and cleans up resources
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Task.Run(StopAsync).Wait();
        }
    }
} 