using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Server
{
    /// <summary>
    /// Local TCP server implementation for MCP connections
    /// </summary>
    public class RhinoMCPServer : IRhinoMCPConnection
    {
        private readonly object lockObject = new object();
        private readonly ConcurrentDictionary<string, ClientConnection> clients = new ConcurrentDictionary<string, ClientConnection>();
        
        private TcpListener tcpListener;
        private CancellationTokenSource cancellationTokenSource;
        private Task serverTask;
        private ConnectionStatus status = ConnectionStatus.Disconnected;
        private bool disposed;
        
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
        public event EventHandler<CommandReceivedEventArgs> CommandReceived;
        
        /// <summary>
        /// Event fired when the connection status changes
        /// </summary>
        public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;
        
        /// <summary>
        /// Starts the connection with the specified settings
        /// </summary>
        /// <param name="settings">Connection settings to use</param>
        /// <returns>True if connection started successfully, false otherwise</returns>
        public async Task<bool> StartAsync(ConnectionSettings settings)
        {
            if (settings == null || settings.Mode != ConnectionMode.Local)
                return false;
                
            if (disposed)
                throw new ObjectDisposedException(nameof(RhinoMCPServer));

            lock (lockObject)
            {
                if (status != ConnectionStatus.Disconnected)
                {
                    RhinoApp.WriteLine("TCP server is already running or starting");
                    return false;
                }

                status = ConnectionStatus.Connecting;
                Settings = settings;
            }

            OnStatusChanged(ConnectionStatus.Connecting, "Starting TCP server...");

            try
            {
                // Create TCP listener
                IPAddress ipAddress = IPAddress.Parse(settings.LocalHost);
                tcpListener = new TcpListener(ipAddress, settings.LocalPort);
                tcpListener.Start();

                // Create cancellation token for server operations
                cancellationTokenSource = new CancellationTokenSource();

                // Start server loop
                serverTask = Task.Run(() => ServerLoopAsync(cancellationTokenSource.Token));

                lock (lockObject)
                {
                    status = ConnectionStatus.Connected;
                }

                OnStatusChanged(ConnectionStatus.Connected, $"TCP server started on {settings.LocalHost}:{settings.LocalPort}");
                RhinoApp.WriteLine($"RhinoMCP TCP server started on {settings.LocalHost}:{settings.LocalPort}");

                return true;
            }
            catch (Exception ex)
            {
                lock (lockObject)
                {
                    status = ConnectionStatus.Failed;
                }

                OnStatusChanged(ConnectionStatus.Failed, $"Failed to start TCP server: {ex.Message}", ex);
                RhinoApp.WriteLine($"Failed to start RhinoMCP TCP server: {ex.Message}");

                await CleanupAsync();
                return false;
            }
        }
        
        /// <summary>
        /// Stops the connection and cleans up resources
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        public async Task StopAsync()
        {
            ConnectionStatus currentStatus;
            lock (lockObject)
            {
                currentStatus = status;
                if (currentStatus == ConnectionStatus.Disconnected)
                    return;

                status = ConnectionStatus.Disconnected;
            }

            OnStatusChanged(ConnectionStatus.Disconnected, "Stopping TCP server...");
            RhinoApp.WriteLine("Stopping RhinoMCP TCP server...");

            await CleanupAsync();

            OnStatusChanged(ConnectionStatus.Disconnected, "TCP server stopped");
            RhinoApp.WriteLine("RhinoMCP TCP server stopped");
        }
        
        /// <summary>
        /// Sends a response back to the client
        /// </summary>
        /// <param name="responseJson">JSON response to send</param>
        /// <param name="clientId">ID of the client to send to (for multi-client scenarios)</param>
        /// <returns>True if response was sent successfully, false otherwise</returns>
        public async Task<bool> SendResponseAsync(string responseJson, string clientId = null)
        {
            if (string.IsNullOrEmpty(responseJson))
                return false;

            try
            {
                if (!string.IsNullOrEmpty(clientId))
                {
                    // Send to specific client
                    if (clients.TryGetValue(clientId, out ClientConnection client))
                    {
                        return await client.SendResponseAsync(responseJson);
                    }
                    else
                    {
                        RhinoApp.WriteLine($"Client {clientId} not found");
                        return false;
                    }
                }
                else
                {
                    // Send to all connected clients
                    bool anySuccess = false;
                    foreach (var client in clients.Values)
                    {
                        if (await client.SendResponseAsync(responseJson))
                        {
                            anySuccess = true;
                        }
                    }
                    return anySuccess;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error sending response: {ex.Message}");
                return false;
            }
        }
        
        private async Task ServerLoopAsync(CancellationToken cancellationToken)
        {
            RhinoApp.WriteLine("TCP server loop started");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check for pending connections with timeout
                        if (tcpListener.Pending())
                        {
                            TcpClient tcpClient = await AcceptTcpClientAsync(tcpListener, cancellationToken);
                            if (tcpClient != null)
                            {
                                _ = Task.Run(() => HandleNewClientAsync(tcpClient), cancellationToken);
                            }
                        }
                        else
                        {
                            // No pending connections, wait a bit
                            await Task.Delay(100, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Error in server loop: {ex.Message}");
                        
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(500, cancellationToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Unexpected error in server loop: {ex.Message}");
            }

            RhinoApp.WriteLine("TCP server loop stopped");
        }
        
        private async Task<TcpClient> AcceptTcpClientAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                // Use Task.Run to make AcceptTcpClient cancellable
                var acceptTask = Task.Run(() => listener.AcceptTcpClient(), cancellationToken);
                return await acceptTask;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error accepting client: {ex.Message}");
                return null;
            }
        }
        
        private async Task HandleNewClientAsync(TcpClient tcpClient)
        {
            ClientConnection clientConnection = null;
            
            try
            {
                clientConnection = new ClientConnection(tcpClient);
                
                // Subscribe to client events
                clientConnection.CommandReceived += OnClientCommandReceived;
                clientConnection.Disconnected += OnClientDisconnected;

                // Add to clients collection
                clients.TryAdd(clientConnection.ClientId, clientConnection);

                // Start handling the client
                await clientConnection.StartAsync();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error handling new client: {ex.Message}");
            }
            finally
            {
                // Clean up client connection
                if (clientConnection != null)
                {
                    clients.TryRemove(clientConnection.ClientId, out _);
                    clientConnection.CommandReceived -= OnClientCommandReceived;
                    clientConnection.Disconnected -= OnClientDisconnected;
                    clientConnection.Dispose();
                }
            }
        }
        
        private void OnClientCommandReceived(object sender, ClientCommandEventArgs e)
        {
            try
            {
                var args = new CommandReceivedEventArgs(
                    e.Command.ToString(),
                    e.Command,
                    e.ClientId
                );

                CommandReceived?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error handling client command: {ex.Message}");
            }
        }
        
        private void OnClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            try
            {
                clients.TryRemove(e.ClientId, out _);
                RhinoApp.WriteLine($"Client {e.ClientId} removed from active connections");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error handling client disconnection: {ex.Message}");
            }
        }
        
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
        
        private async Task CleanupAsync()
        {
            try
            {
                // Cancel server operations
                cancellationTokenSource?.Cancel();

                // Stop TCP listener
                tcpListener?.Stop();

                // Disconnect all clients
                foreach (var client in clients.Values)
                {
                    try
                    {
                        client.Stop();
                        client.Dispose();
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Error disposing client: {ex.Message}");
                    }
                }
                clients.Clear();

                // Wait for server task to complete
                if (serverTask != null)
                {
                    try
                    {
                        await serverTask.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Error waiting for server task: {ex.Message}");
                    }
                }

                // Dispose resources
                cancellationTokenSource?.Dispose();
                tcpListener = null;
                cancellationTokenSource = null;
                serverTask = null;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes the server and cleans up resources
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            try
            {
                // Stop server synchronously with timeout
                var stopTask = StopAsync();
                stopTask.Wait(5000);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error disposing RhinoMCPServer: {ex.Message}");
            }
        }
    }
} 