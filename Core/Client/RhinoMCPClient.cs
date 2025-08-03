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
        /// Check if the current session is still valid on the server
        /// </summary>
        /// <returns>True if session is valid and active, false otherwise</returns>
        public async Task<bool> IsCurrentSessionValidAsync()
        {
            if (string.IsNullOrEmpty(sessionId) || Settings?.RemoteUrl == null)
                return false;
                
            try
            {
                var response = await httpClient.GetAsync($"{Settings.RemoteUrl}/sessions/{sessionId}/status");
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
                string websocketUrl = await ConnectToSessionAsync(licenseValidation);
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
        /// Connects to an existing session on the remote server that was created by the host app
        /// Session must already exist - plugin cannot create sessions
        /// </summary>
        private async Task<string> ConnectToSessionAsync(LicenseValidationResult licenseInfo)
        {
            try
            {
                // Get current file path
                currentFilePath = GetCurrentRhinoFilePath();
                
                Logger.Info($"Connecting to existing session for file: {Path.GetFileName(currentFilePath)}");
                
                // Try to connect to existing session
                var connectionResult = await TryConnectToExistingSessionAsync(licenseInfo, currentFilePath);
                if (connectionResult != null)
                {
                    Logger.Success($"✓ Connected to existing session - ID: {connectionResult.SessionId}");
                    sessionId = connectionResult.SessionId;
                    instanceId = connectionResult.InstanceId;
                    
                    // Update local cache with session info including document GUID
                    var documentGuid = DocumentGUIDHelper.GetOrCreateDocumentGUID();
                    await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.RegisterLinkedFileAsync(sessionId, currentFilePath, documentGuid);
                    
                    return connectionResult.WebSocketUrl;
                }
                
                // No session found - provide clear guidance
                var fileName = Path.GetFileName(currentFilePath);
                throw new Exception($"No session found for '{fileName}'. Please ensure:\n" +
                    "1. The host application has created a session for this file\n" +
                    "2. The file path matches exactly (case-sensitive)\n" +
                    "3. The session has not expired or been deactivated\n" +
                    "4. Your license is valid and matches the session license");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error connecting to session: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Try to connect to an existing session on the server using GUID-based approach
        /// </summary>
        private async Task<ExistingSessionInfo> TryConnectToExistingSessionAsync(LicenseValidationResult licenseInfo, string filePath)
        {
            try
            {
                // Get or create document GUID for current file
                var documentGuid = DocumentGUIDHelper.GetOrCreateDocumentGUID();
                
                // Perform comprehensive file validation
                var validation = await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.ValidateFileForConnectionAsync(filePath, documentGuid);
                
                // Handle different validation scenarios
                switch (validation.ValidationScenario)
                {
                    case FileValidationScenario.PerfectMatch:
                        Logger.Info("File validation passed - perfect match found");
                        // Try to reconnect using cached session
                        var reconnectResult = await TryReconnectToServerSessionAsync(licenseInfo, validation.SessionId);
                        if (reconnectResult != null)
                        {
                            Logger.Success($"Reconnected to existing session (GUID: {documentGuid})");
                            return reconnectResult;
                        }
                        // Session no longer valid on server, clean up
                        await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.UnregisterLinkedFileAsync(validation.SessionId);
                        break;
                        
                    case FileValidationScenario.FilePathChanged:
                        Logger.Info($"File has been moved/renamed. Original: {validation.LinkedFileInfo.FilePath}, Current: {filePath}");
                        // Update the file path in linked file info
                        validation.LinkedFileInfo.FilePath = filePath;
                        validation.LinkedFileInfo.Status = FileStatus.PathChanged;
                        await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.RegisterLinkedFileAsync(
                            validation.SessionId, filePath, documentGuid);
                        
                        // Try to reconnect
                        var pathChangedResult = await TryReconnectToServerSessionAsync(licenseInfo, validation.SessionId);
                        if (pathChangedResult != null)
                        {
                            Logger.Success("Reconnected after file path change");
                            return pathChangedResult;
                        }
                        await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.UnregisterLinkedFileAsync(validation.SessionId);
                        break;
                        
                    case FileValidationScenario.LegacyFile:
                        Logger.Info("Legacy file without GUID detected, updating with new GUID");
                        // Update legacy file with new GUID
                        await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.RegisterLinkedFileAsync(
                            validation.LinkedFileInfo.SessionId, filePath, documentGuid);
                        
                        var legacyResult = await TryReconnectToServerSessionAsync(licenseInfo, validation.LinkedFileInfo.SessionId);
                        if (legacyResult != null)
                        {
                            Logger.Success("Reconnected to legacy session with updated GUID");
                            return legacyResult;
                        }
                        await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.UnregisterLinkedFileAsync(validation.LinkedFileInfo.SessionId);
                        break;
                        
                    case FileValidationScenario.FileReplacedNoGUID:
                        Logger.Warning($"File at {filePath} has no document GUID but was previously linked with GUID");
                        Logger.Warning("This suggests the file has been replaced.");
                        Logger.Warning("Original GUID: " + validation.LinkedFileInfo.DocumentGUID);
                        
                        // Let ReerStartCommand handle the user prompt and cleanup
                        return null;
                        
                    case FileValidationScenario.FileReplaced:
                        Logger.Warning($"File at {filePath} appears to be replaced (different GUID)");
                        Logger.Warning("Original GUID: " + validation.LinkedFileInfo.DocumentGUID);
                        Logger.Warning("Current GUID: " + documentGuid);
                        
                        // Clean up the old linked file info and require new session
                        await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.UnregisterLinkedFileAsync(validation.LinkedFileInfo.SessionId);
                        Logger.Info("Cleaned up old session. New session required.");
                        return null;
                        
                    case FileValidationScenario.NoLinkFound:
                        Logger.Info("No existing link found for this file");
                        break;
                        
                    case FileValidationScenario.ValidationError:
                        Logger.Error($"File validation error: {validation.Message}");
                        break;
                }
                
                // Try to connect using the new /sessions/connect endpoint
                return await TryConnectToServerSessionDirectAsync(licenseInfo, filePath, documentGuid);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error connecting to existing session: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Try to connect to session using the new /sessions/connect endpoint with GUID support
        /// </summary>
        private async Task<ExistingSessionInfo> TryConnectToServerSessionDirectAsync(LicenseValidationResult licenseInfo, string filePath, string documentGuid = null)
        {
            try
            {
                var connectRequest = new
                {
                    user_id = licenseInfo.UserId,
                    file_path = filePath,
                    license_id = licenseInfo.LicenseId,
                    document_guid = documentGuid
                };

                var jsonContent = JsonConvert.SerializeObject(connectRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{Settings.RemoteUrl}/sessions/connect", content);
                
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
                    WebSocketUrl = sessionData["websocket_url"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error connecting to session via direct endpoint: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to reconnect to an existing session on the server using cached session ID
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
                    // Sanitize error message to prevent information disclosure
                    var sanitizedError = SanitizeErrorMessage(errorContent);
                    Logger.Error($"Session reconnection failed: {response.StatusCode} - {sanitizedError}");
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