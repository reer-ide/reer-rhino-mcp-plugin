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

namespace ReerRhinoMCPPlugin.Core.Client
{
    /// <summary>
    /// Remote WebSocket client implementation for MCP connections
    /// </summary>
    public class RhinoMCPClient : IRhinoMCPConnection, IDisposable
    {
        private readonly object lockObject = new object();
        private readonly ToolExecutor toolExecutor;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly HttpClient httpClient;
        
        private ClientWebSocket webSocket;
        private Task receiveTask;
        private bool disposed;
        private ConnectionStatus status = ConnectionStatus.Disconnected;
        
        // Thread-safe property for status access
        public ConnectionStatus Status
        {
            get
            {
                lock (lockObject)
                {
                    return status;
                }
            }
            private set
            {
                lock (lockObject)
                {
                    status = value;
                }
            }
        }
        private string sessionId;
        private string instanceId;
        private string licenseId;
        private string currentFilePath;

        /// <summary>
        /// Information about an existing session that can be reused
        /// </summary>
        public class ExistingSessionInfo
        {
            public string SessionId { get; set; }
            public string InstanceId { get; set; }
            public string WebSocketUrl { get; set; }
            public string DocumentGuid { get; set; }  // Server-managed document GUID
        }

        public RhinoMCPClient()
        {
            toolExecutor = new ToolExecutor();
            cancellationTokenSource = new CancellationTokenSource();
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // Cross-platform timeout
            };
        }
        
        /// <summary>
        /// Indicates whether the connection is currently active
        /// </summary>
        public bool IsConnected => Status == ConnectionStatus.Connected;
        
        /// <summary>
        /// The connection settings being used
        /// </summary>
        public ConnectionSettings Settings { get; private set; }
        
        /// <summary>
        /// Current session ID (null if not connected to a session)
        /// </summary>
        public string CurrentSessionId => sessionId;
        
        /// <summary>
        /// Current file path being used for the session
        /// </summary>
        public string CurrentFilePath => currentFilePath;
        
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
        
        
#region connection management
        /// <summary>
        /// Starts the connection with the specified settings
        /// </summary>
        /// <param name="settings">Connection settings to use</param>
        /// <param name="fileValidation">Optional pre-computed file validation result to avoid duplicate validation</param>
        /// <returns>True if connection started successfully, false otherwise</returns>
        public async Task<bool> StartAsync(ConnectionSettings settings, FileConnectionValidation fileValidation)
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

                Status = ConnectionStatus.Connecting;
                Settings = settings;
            }

            OnStatusChanged(ConnectionStatus.Connecting, "Validating license and creating session...");

            try
            {
                // Step 1: Validate license (automatically syncs with server and clears local cache if invalid)
                var licenseValidation = await ReerRhinoMCPPlugin.Instance.LicenseManager.ValidateLicenseAsync();
                if (!licenseValidation.IsValid)
                {
                    throw new Exception($"License validation failed: {licenseValidation.Message}");
                }

                licenseId = licenseValidation.LicenseId;
                Logger.Success($"✓ License validated: {licenseId} (Tier: {licenseValidation.Tier})");

                // Step 2: Connect to existing session on remote server
                string websocketUrl = await ConnectToSessionAsync(licenseValidation, fileValidation);
                if (string.IsNullOrEmpty(websocketUrl))
                {
                    throw new Exception("Failed to connect to existing session or get WebSocket URL");
                }

                // Step 3: Connect to WebSocket with timeout and retry logic
                webSocket = new ClientWebSocket();

                // Cross-platform compatible connection timeout
                var connectTimeout = TimeSpan.FromSeconds(30);
                var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                connectCts.CancelAfter(connectTimeout);

                var uri = new Uri(websocketUrl);

                // Retry logic for connection
                int maxRetries = 3;
                int retryDelay = 1000; // Start with 1 second

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        await webSocket.ConnectAsync(uri, connectCts.Token);
                        break; // Success, exit retry loop
                    }
                    catch (OperationCanceledException) when (connectCts.Token.IsCancellationRequested)
                    {
                        throw new TimeoutException($"WebSocket connection timeout after {connectTimeout.TotalSeconds} seconds");
                    }
                    catch (Exception ex) when (attempt < maxRetries - 1)
                    {
                        Logger.Warning($"Connection attempt {attempt + 1} failed: {ex.Message}. Retrying in {retryDelay}ms...");
                        await Task.Delay(retryDelay, cancellationTokenSource.Token);
                        retryDelay *= 2; // Exponential backoff

                        // Create new WebSocket for retry (cross-platform best practice)
                        webSocket?.Dispose();
                        webSocket = new ClientWebSocket();

                        // Reset timeout for next attempt
                        connectCts?.Dispose();
                        connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                        connectCts.CancelAfter(connectTimeout);
                    }
                }

                Status = ConnectionStatus.Connected;

                // Step 4: Start receiving messages
                receiveTask = Task.Run(() => ReceiveMessagesAsync(cancellationTokenSource.Token));

                OnStatusChanged(ConnectionStatus.Connected, $"WebSocket client connected to {websocketUrl}");
                Logger.Success($"✓ RhinoMCP WebSocket client connected successfully");

                return true;
            }
            catch (Exception ex)
            {
                Status = ConnectionStatus.Failed;

                // Consolidate error message - avoid duplicate logging
                string errorMessage = ex.Message;
                if (errorMessage.StartsWith("No session found"))
                {
                    // Add detailed help for session not found errors
                    errorMessage = $"{ex.Message}\nPossible reasons:\n" +
                        "• Host application hasn't created a session for this file\n" +
                        "• Session has expired or been deactivated\n" +
                        "• File has been modified since session creation\n" +
                        "• License validation failed";
                }
                OnStatusChanged(ConnectionStatus.Failed, errorMessage, ex);
                Logger.Error($"✗ Failed to connect: {ex.Message}");

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

                Status = ConnectionStatus.Disconnected;
            }

            OnStatusChanged(ConnectionStatus.Disconnected, "Stopping WebSocket client...");
            Logger.Info("Stopping RhinoMCP WebSocket client...");

            // Unregister file if we have a session and cleanSessionInfo is true
            if (cleanSessionInfo && !string.IsNullOrEmpty(sessionId))
            {
                await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.UnregisterLinkedFileAsync(sessionId);
            }

            await CleanupAsync();

            // Clear session information if requested
            if (cleanSessionInfo)
            {
                sessionId = null;
                instanceId = null;
                licenseId = null;
                currentFilePath = null;
            }

            OnStatusChanged(ConnectionStatus.Disconnected, "WebSocket client stopped");
            Logger.Info("RhinoMCP WebSocket client stopped");
        }
        
        /// <summary>
        /// Check if the current session is still valid on the server
        /// </summary>
        /// <returns>True if session is valid and active, false otherwise</returns>
        public async Task<bool> IsCurrentSessionValidAsync()
        {
            if (string.IsNullOrEmpty(sessionId) || Settings?.RemoteUrl == null)
                return false;
                
            try
            {
                var response = await httpClient.GetAsync($"{Settings.RemoteHttpUrl}/sessions/{sessionId}/status");
                if (response.IsSuccessStatusCode)
                {
                    var statusData = await response.Content.ReadAsStringAsync();
                    var sessionStatus = JsonConvert.DeserializeObject<JObject>(statusData);
                    var serverStatus = sessionStatus["status"]?.ToString();
                    var serverInstanceId = sessionStatus["instance_id"]?.ToString();
                    
                    // Session is valid if it's active and has our instance ID
                    return serverStatus == "active" && serverInstanceId == instanceId;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not validate session status: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Connects to an existing session on the remote server based on file validation
        /// </summary>
        private async Task<string> ConnectToSessionAsync(LicenseValidationResult licenseInfo, FileConnectionValidation fileValidation)
        {
            try
            {
                // Get current file path
                currentFilePath = GetCurrentRhinoFilePath();
                Logger.Info($"Connecting to existing session for file: {Path.GetFileName(currentFilePath)}");
                
                // Determine connection parameters based on validation scenario
                string searchSessionId = null;
                string searchDocumentGuid = null; 
                string searchFilePath = null;
                
                switch (fileValidation.ValidationScenario)
                {
                    case FileValidationScenario.PerfectMatch:
                        Logger.Info("File validation passed - perfect match found");
                        searchSessionId = fileValidation.SessionId;
                        break;
                        
                    case FileValidationScenario.FilePathChanged:
                        Logger.Info($"File has been moved/renamed. Original: {fileValidation.LinkedFileInfo.FilePath}, Current: {currentFilePath}");
                        searchSessionId = fileValidation.SessionId;
                        // IMPORTANT: Also send the new file path so server can update its records
                        searchFilePath = currentFilePath;
                        break;
                        
                    case FileValidationScenario.FileReplaced:
                        Logger.Info("File was replaced - continuing with existing session");
                        searchSessionId = fileValidation.SessionId;
                        // Also send the current path to ensure server has correct path
                        searchFilePath = currentFilePath;
                        break;
                        
                    case FileValidationScenario.NoLinkFound:
                        Logger.Info("No existing link found for this file - trying server connection");
                        searchFilePath = currentFilePath;
                        break;
                        
                    default:
                        Logger.Warning($"Unknown validation scenario: {fileValidation.ValidationScenario}");
                        return null;
                }
                
                // Try to connect to server
                var connectionResult = await ConnectToServerAsync(licenseInfo, searchSessionId, searchDocumentGuid, searchFilePath);
                if (connectionResult == null)
                {
                    // Clean up invalid session if we had one
                    if (!string.IsNullOrEmpty(fileValidation.SessionId))
                    {
                        await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.UnregisterLinkedFileAsync(fileValidation.SessionId);
                    }
                    
                    var fileName = Path.GetFileName(currentFilePath);
                    // Don't log here - let the caller handle it
                    throw new Exception($"No session found for '{fileName}'.");
                }
                
                // Connection successful - update instance variables
                sessionId = connectionResult.SessionId;
                instanceId = connectionResult.InstanceId;
                
                // Register or update the linked file in storage
                // This will replace any existing entry with the same sessionId, effectively updating the path
                await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.RegisterLinkedFileAsync(
                    sessionId, currentFilePath, connectionResult.DocumentGuid);
                
                Logger.Success($"✓ Connected to existing session - ID: {sessionId}");
                Logger.Info($"Using server-provided document GUID: {connectionResult.DocumentGuid}");
                
                return connectionResult.WebSocketUrl;
            }
            catch (Exception)
            {
                // Don't log here - let the caller handle it to avoid duplicate logs
                throw;
            }
        }
        
        
        /// <summary>
        /// Connect to server session using various search criteria
        /// </summary>
        private async Task<ExistingSessionInfo> ConnectToServerAsync(LicenseValidationResult licenseInfo, string sessionId = null, string documentGuid = null, string filePath = null)
        {
            try
            {
                var connectRequest = new
                {
                    user_id = licenseInfo.UserId,
                    license_id = licenseInfo.LicenseId,
                    session_id = sessionId,
                    document_guid = documentGuid,
                    file_path = filePath
                };

                var jsonContent = JsonConvert.SerializeObject(connectRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{Settings.RemoteHttpUrl}/sessions/connect", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Logger.Warning($"No session exists for this file: {Path.GetFileName(filePath)}");
                        Logger.Info("The host application must create a session for this file first");
                        return null;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Gone) // 410 - Session expired
                    {
                        Logger.Warning("Session has expired on server (30-day limit exceeded)");
                        Logger.Info("Ask the host application to create a new session");
                        return null;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) // 403 - License mismatch
                    {
                        Logger.Error("License validation failed - your license doesn't match the session");
                        return null;
                    }
                    
                    // Sanitize error message to prevent information disclosure
                    var sanitizedError = SanitizeErrorMessage(errorContent);
                    Logger.Error($"Session connection failed: {response.StatusCode} - {sanitizedError}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var sessionData = JsonConvert.DeserializeObject<JObject>(responseJson);

                return new ExistingSessionInfo
                {
                    SessionId = sessionData["session_id"]?.ToString(),
                    InstanceId = sessionData["instance_id"]?.ToString(),
                    WebSocketUrl = sessionData["websocket_url"]?.ToString(),
                    DocumentGuid = sessionData["document_guid"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                // Log at debug level only to avoid duplicate error logs
                Logger.Debug($"Direct endpoint connection failed: {ex.Message}");
                return null;
            }
        }
#endregion


#region message handling
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
        #endregion

        #region Utils

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
        /// Sanitize error messages to prevent information disclosure (cross-platform)
        /// </summary>
        private static string SanitizeErrorMessage(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return "Unknown error";
            
            // Remove potentially sensitive information
            var sensitive = new[] {
                "password", "token", "key", "secret", "credential",
                "internal", "stack", "exception", "debug", "trace",
                "localhost", "127.0.0.1", "::1", "path", "directory"
            };
            
            var sanitized = errorMessage.ToLowerInvariant();
            foreach (var term in sensitive)
            {
                if (sanitized.Contains(term))
                {
                    return "Server error occurred (details withheld for security)";
                }
            }
            
            // Limit length to prevent excessive logging
            if (errorMessage.Length > 200)
            {
                return errorMessage.Substring(0, 200) + "...";
            }
            
            return errorMessage;
        }
        
        /// <summary>
        /// Disposes the client and cleans up resources
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            
            // Cross-platform safe disposal without blocking
            try
            {
                cancellationTokenSource?.Cancel();
                if (webSocket?.State == WebSocketState.Open)
                {
                    // Fire and forget - don't block on close
                    _ = Task.Run(async () => 
                    {
                        try { await StopAsync(); }
                        catch { /* Ignore disposal errors */ }
                    });
                }
                httpClient?.Dispose();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during disposal: {ex.Message}");
            }
        }
    }
#endregion
} 