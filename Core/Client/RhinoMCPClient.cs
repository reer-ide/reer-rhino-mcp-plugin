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
using ReerRhinoMCPPlugin.Core;

namespace ReerRhinoMCPPlugin.Core.Client
{
    /// <summary>
    /// Remote WebSocket client implementation for MCP connections
    /// </summary>
    public class RhinoMCPClient : IRhinoMCPConnection
    {
        private readonly object lockObject = new object();
        private readonly ToolExecutor toolExecutor;
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
            toolExecutor = new ToolExecutor();
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
                Logger.Info("=== RhinoMCP License Registration ===");
                
                var result = await licenseManager.RegisterLicenseAsync(licenseKey, userId, serverUrl);
                
                if (result.Success)
                {
                    Logger.Success($"✓ License registration successful!");
                    Logger.Info($"  License ID: {result.LicenseId}");
                    Logger.Info($"  Tier: {result.Tier}");
                    Logger.Info($"  Max concurrent files: {result.MaxConcurrentFiles}");
                    Logger.Info("  License stored securely for future use");
                    return true;
                }
                else
                {
                    Logger.Error($"✗ License registration failed: {result.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"License registration error: {ex.Message}");
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
            Logger.Info("Stored license cleared. You will need to register again.");
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
            Logger.Info("All linked files cleared.");
        }
        
        /// <summary>
        /// Clean up expired sessions (sessions older than specified hours)
        /// Note: Session expiration is now managed by the remote server (30 days)
        /// </summary>
        /// <param name="expiredHours">Number of hours after which a session is considered expired</param>
        /// <returns>Number of sessions cleaned up</returns>
        public async Task<int> CleanupExpiredSessionsAsync(int expiredHours = 24)
        {
            return await fileIntegrityManager.CleanupExpiredSessionsAsync(expiredHours);
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
                
                Logger.Info($"Reported {statusChanges.Count} file status changes to server");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reporting file status changes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Starts the connection with the specified settings
        /// </summary>
        /// <param name="settings">Connection settings to use</param>
        /// <returns>True if connection started successfully, false otherwise</returns>
        public async Task<bool> StartAsync(ConnectionSettings settings)
        {
            if (settings == null || settings.Mode != ConnectionMode.Remote)
                return false;
                
            if (disposed)
                throw new ObjectDisposedException(nameof(RhinoMCPClient));

            lock (lockObject)
            {
                if (status != ConnectionStatus.Disconnected)
                {
                    Logger.Info("WebSocket client is already running or starting");
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
                Logger.Success($"✓ License validated: {licenseId} (Tier: {licenseValidation.Tier})");

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
                Logger.Success($"✓ RhinoMCP WebSocket client connected successfully");

                return true;
            }
            catch (Exception ex)
            {
                lock (lockObject)
                {
                    status = ConnectionStatus.Failed;
                }

                OnStatusChanged(ConnectionStatus.Failed, $"Failed to connect WebSocket client: {ex.Message}", ex);
                Logger.Error($"✗ Failed to connect RhinoMCP WebSocket client: {ex.Message}");

                await CleanupAsync();
                return false;
            }
        }
        
        /// <summary>
        /// Stops the connection and cleans up resources
        /// </summary>
        /// <param name="cleanSessionInfo">Whether to clean stored session info (default: true)</param>
        /// <returns>Task representing the async operation</returns>
        public async Task StopAsync(bool cleanSessionInfo = true)
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
            Logger.Info("Stopping RhinoMCP WebSocket client...");

            // Unregister file if we have a session and cleanSessionInfo is true
            if (cleanSessionInfo && !string.IsNullOrEmpty(sessionId))
            {
                await fileIntegrityManager.UnregisterLinkedFileAsync(sessionId);
            }

            await CleanupAsync();

            // Clear session information if requested
            if (cleanSessionInfo)
            {
                sessionId = null;
                instanceId = null;
                licenseId = null;
                currentFilePath = null;
                currentFileHash = null;
            }

            OnStatusChanged(ConnectionStatus.Disconnected, "WebSocket client stopped");
            Logger.Info("RhinoMCP WebSocket client stopped");
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
                Logger.Error($"Error sending WebSocket response: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates or reuses a session with the remote server using license-based authentication
        /// Session expiration is managed by the remote server (30 days)
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
                
                Logger.Success($"✓ File hash calculated: {currentFileHash?.Substring(0, 16)}...");
                
                // First, check if we have an existing valid session for this file
                var existingSession = await TryReconnectToExistingSessionAsync(licenseInfo, currentFilePath, currentFileHash);
                if (existingSession != null)
                {
                    Logger.Success($"✓ Reusing existing session - ID: {existingSession.SessionId}");
                    sessionId = existingSession.SessionId;
                    instanceId = existingSession.InstanceId;
                    return existingSession.WebSocketUrl;
                }
                
                // No valid existing session, create a new one
                Logger.Info("Creating new session...");
                return await CreateNewSessionAsync(licenseInfo, currentFilePath, currentFileHash, fileSize);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating/reusing session: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Try to reconnect to an existing session for the current file
        /// </summary>
        private async Task<ExistingSessionInfo> TryReconnectToExistingSessionAsync(LicenseValidationResult licenseInfo, string filePath, string fileHash)
        {
            try
            {
                // Check if we have any linked files for this file path
                var linkedFiles = fileIntegrityManager.GetAllLinkedFiles();
                var potentialSession = linkedFiles.FirstOrDefault(f => 
                    string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase) &&
                    f.Status == FileStatus.Available);
                
                if (potentialSession == null)
                {
                    Logger.Info("No existing session found for this file");
                    return null;
                }
                
                // Validate file integrity
                var validationResult = await fileIntegrityManager.ValidateFileForReconnectionAsync(
                    potentialSession.SessionId, filePath, fileHash);
                
                if (!validationResult.IsValid)
                {
                    Logger.Error($"File validation failed: {validationResult.Message}");
                    // Clean up invalid session
                    await fileIntegrityManager.UnregisterLinkedFileAsync(potentialSession.SessionId);
                    return null;
                }
                
                // Try to reconnect to the existing session
                var reconnectResult = await TryReconnectToServerSessionAsync(licenseInfo, potentialSession.SessionId);
                if (reconnectResult != null)
                {
                    return reconnectResult;
                }
                
                // Session is no longer valid on server, clean up
                await fileIntegrityManager.UnregisterLinkedFileAsync(potentialSession.SessionId);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking existing session: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Try to reconnect to an existing session on the server
        /// </summary>
        private async Task<ExistingSessionInfo> TryReconnectToServerSessionAsync(LicenseValidationResult licenseInfo, string sessionId)
        {
            try
            {
                var reconnectRequest = new
                {
                    session_id = sessionId,
                    license_id = licenseInfo.LicenseId,
                    user_id = licenseInfo.UserId
                };

                var jsonContent = JsonConvert.SerializeObject(reconnectRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{Settings.RemoteUrl}/sessions/reconnect", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Logger.Info("Session no longer exists on server");
                        return null;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Gone) // 410 - Session expired
                    {
                        Logger.Info("Session has expired on server (server manages 30-day expiration)");
                        return null;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) // 403 - License/User mismatch
                    {
                        Logger.Info("License or user validation failed for session");
                        return null;
                    }
                    
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Logger.Error($"Session reconnection failed: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var sessionData = JsonConvert.DeserializeObject<JObject>(responseJson);

                return new ExistingSessionInfo
                {
                    SessionId = sessionData["session_id"]?.ToString(),
                    InstanceId = sessionData["instance_id"]?.ToString(),
                    WebSocketUrl = sessionData["websocket_url"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reconnecting to session: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Create a new session on the server
        /// </summary>
        private async Task<string> CreateNewSessionAsync(LicenseValidationResult licenseInfo, string filePath, string fileHash, long fileSize)
        {
            // Create session request with license authentication and file info
            var sessionRequest = new
            {
                user_id = licenseInfo.UserId,
                file_path = filePath,
                file_hash = fileHash,
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
            await fileIntegrityManager.RegisterLinkedFileAsync(sessionId, filePath, fileHash);

            Logger.Success($"✓ New session created - ID: {sessionId}");
            Logger.Info($"  File: {Path.GetFileName(filePath)}");
            Logger.Info($"  File size: {fileSize:N0} bytes");
            
            return websocketUrl;
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
                Logger.Warning($"Warning: Could not get current file path: {ex.Message}");
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
                        Logger.Info("WebSocket connection closed by server");
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
                Logger.Error($"Error receiving WebSocket messages: {ex.Message}");
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

                Logger.Info($"Received message type: {messageType}");

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
                        Logger.Info($"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing incoming message: {ex.Message}");
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
            
            Logger.Success($"✓ Handshake completed");
            Logger.Info($"  Session: {sessionId}");
            Logger.Info($"  Instance: {instanceId}");
            Logger.Info($"  File: {filePath}");
            
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

                Logger.Info($"Processing command: {tool} with correlation ID: {correlationId}");

                // Fire command received event
                var commandArgs = new CommandReceivedEventArgs(data.ToString(), data, instanceId ?? "unknown");
                CommandReceived?.Invoke(this, commandArgs);

                // Process command using the tool executor
                string result = await Task.Run(() => toolExecutor.ProcessTool(new JObject
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
                // For capture_rhino_viewport, do not log the whole result's image data
                if (tool == "capture_rhino_viewport")
                {
                    Logger.Info($"Response sent: {response.ToString().Substring(0, 150)}...");
                }
                else
                {
                    Logger.Info($"Response sent: {response.ToString()}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling command: {ex.Message}");
                
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
            // Logger.Info("Heartbeat response sent"); // Comment out to reduce noise
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
                Logger.Error($"Error in status changed handler: {ex.Message}");
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
                Logger.Error($"Error during cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes the client and cleans up resources
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Task.Run(() => StopAsync()).Wait();
        }
    }
    
    /// <summary>
    /// Information about an existing session that can be reused
    /// </summary>
    public class ExistingSessionInfo
    {
        public string SessionId { get; set; }
        public string InstanceId { get; set; }
        public string WebSocketUrl { get; set; }
    }
} 