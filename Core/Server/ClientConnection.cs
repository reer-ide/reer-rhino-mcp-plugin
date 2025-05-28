using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;

namespace ReerRhinoMCPPlugin.Core.Server
{
    /// <summary>
    /// Represents a single client connection to the MCP server
    /// </summary>
    internal class ClientConnection : IDisposable
    {
        private readonly TcpClient tcpClient;
        private readonly NetworkStream stream;
        private readonly string clientId;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly object lockObject = new object();
        private bool disposed;
        private bool isRunning;

        /// <summary>
        /// Unique identifier for this client connection
        /// </summary>
        public string ClientId => clientId;

        /// <summary>
        /// Indicates whether the client is still connected
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (lockObject)
                {
                    return !disposed && tcpClient?.Connected == true && isRunning;
                }
            }
        }

        /// <summary>
        /// Event fired when a command is received from this client
        /// </summary>
        public event EventHandler<ClientCommandEventArgs> CommandReceived;

        /// <summary>
        /// Event fired when this client disconnects
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> Disconnected;

        public ClientConnection(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            this.stream = tcpClient.GetStream();
            this.clientId = Guid.NewGuid().ToString("N").Substring(0, 8); // Short ID for logging
            this.cancellationTokenSource = new CancellationTokenSource();
            this.isRunning = false;
        }

        /// <summary>
        /// Starts handling this client connection
        /// </summary>
        public async Task StartAsync()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ClientConnection));

            lock (lockObject)
            {
                if (isRunning)
                    return;
                isRunning = true;
            }

            RhinoApp.WriteLine($"Client {clientId} connected from {tcpClient.Client.RemoteEndPoint}");

            try
            {
                await HandleClientAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error handling client {clientId}: {ex.Message}");
            }
            finally
            {
                lock (lockObject)
                {
                    isRunning = false;
                }
                
                OnDisconnected();
            }
        }

        /// <summary>
        /// Sends a response to this client
        /// </summary>
        public async Task<bool> SendResponseAsync(string responseJson)
        {
            if (disposed || !IsConnected)
                return false;

            try
            {
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationTokenSource.Token);
                await stream.FlushAsync(cancellationTokenSource.Token);
                return true;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Failed to send response to client {clientId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the client connection
        /// </summary>
        public void Stop()
        {
            lock (lockObject)
            {
                if (!isRunning)
                    return;
                isRunning = false;
            }

            cancellationTokenSource.Cancel();
        }

        private async Task HandleClientAsync(CancellationToken cancellationToken)
        {
            const int bufferSize = 8192;
            byte[] buffer = new byte[bufferSize];
            string incompleteData = string.Empty;

            try
            {
                while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                {
                    // Check if data is available
                    if (!stream.DataAvailable)
                    {
                        await Task.Delay(50, cancellationToken);
                        continue;
                    }

                    int bytesRead = await stream.ReadAsync(buffer, 0, bufferSize, cancellationToken);
                    if (bytesRead == 0)
                    {
                        RhinoApp.WriteLine($"Client {clientId} disconnected");
                        break;
                    }

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    incompleteData += data;

                    // Try to parse complete JSON messages
                    await ProcessIncomingData(incompleteData, cancellationToken);
                    incompleteData = string.Empty; // Reset after processing
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (IOException ex)
            {
                RhinoApp.WriteLine($"IO error with client {clientId}: {ex.Message}");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Unexpected error with client {clientId}: {ex.Message}");
            }
        }

        private async Task ProcessIncomingData(string data, CancellationToken cancellationToken)
        {
            try
            {
                // Try to parse as JSON
                JObject command = JObject.Parse(data);
                
                // Fire command received event on UI thread
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    try
                    {
                        CommandReceived?.Invoke(this, new ClientCommandEventArgs(command, clientId));
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Error in command received handler: {ex.Message}");
                    }
                }));
            }
            catch (JsonException)
            {
                // Invalid or incomplete JSON - send error response
                string errorResponse = JsonConvert.SerializeObject(new
                {
                    status = "error",
                    message = "Invalid JSON format"
                });

                await SendResponseAsync(errorResponse);
            }
        }

        private void OnDisconnected()
        {
            try
            {
                Disconnected?.Invoke(this, new ClientDisconnectedEventArgs(clientId));
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error in disconnected handler: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            try
            {
                Stop();
                cancellationTokenSource?.Cancel();
                stream?.Close();
                tcpClient?.Close();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error disposing client {clientId}: {ex.Message}");
            }

            RhinoApp.WriteLine($"Client {clientId} disposed");
        }
    }

    /// <summary>
    /// Event arguments for client command events
    /// </summary>
    internal class ClientCommandEventArgs : EventArgs
    {
        public JObject Command { get; }
        public string ClientId { get; }

        public ClientCommandEventArgs(JObject command, string clientId)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        }
    }

    /// <summary>
    /// Event arguments for client disconnection events
    /// </summary>
    internal class ClientDisconnectedEventArgs : EventArgs
    {
        public string ClientId { get; }

        public ClientDisconnectedEventArgs(string clientId)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        }
    }
} 