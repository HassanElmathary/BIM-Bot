using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Core
{
    /// <summary>
    /// TCP Socket server that listens for MCP commands from the MCP server.
    /// Uses JSON-RPC 2.0 protocol for communication.
    /// 
    /// BUG FIXES:
    /// - Message framing: uses newline delimiter to separate JSON-RPC messages
    /// - AcceptTcpClientAsync: .NET 4.8 doesn't support CancellationToken overload, 
    ///   so we use polling with pending check
    /// - Proper client cleanup on errors
    /// </summary>
    public class SocketService
    {
        private TcpListener? _listener;
        private readonly int _port;
        private readonly ExternalEventManager _eventManager;
        private CancellationTokenSource? _cts;
        private readonly List<TcpClient> _clients = new List<TcpClient>();

        public bool IsRunning { get; private set; }

        public SocketService(int port, ExternalEventManager eventManager)
        {
            _port = port;
            _eventManager = eventManager;
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, _port);
                _listener.Start();
                IsRunning = true;

                Task.Run(() => AcceptClientsAsync(_cts.Token));
                Logger.Log($"Socket service started on port {_port}");
            }
            catch (SocketException ex)
            {
                Logger.LogError($"Failed to start socket service on port {_port} — port may be in use", ex);
                IsRunning = false;
                throw;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _cts?.Cancel();

            lock (_clients)
            {
                foreach (var client in _clients)
                {
                    try { client.Close(); } catch { }
                }
                _clients.Clear();
            }

            try { _listener?.Stop(); } catch { }
            IsRunning = false;
            Logger.Log("Socket service stopped");
        }

        private async Task AcceptClientsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // .NET 4.8 AcceptTcpClientAsync doesn't take a CancellationToken.
                    // Use Pending() check with delay to allow cancellation.
                    if (!_listener!.Pending())
                    {
                        await Task.Delay(100, ct);
                        continue;
                    }

                    var client = await _listener.AcceptTcpClientAsync();
                    client.ReceiveTimeout = 0;  // No timeout for long-lived connection
                    client.SendTimeout = 30000; // 30s write timeout

                    Logger.Log("MCP Server connected");
                    lock (_clients) { _clients.Add(client); }
                    _ = Task.Run(() => HandleClientAsync(client, ct));
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        Logger.LogError("Error accepting client", ex);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            var buffer = new byte[65536];
            var messageBuffer = new StringBuilder();

            try
            {
                var stream = client.GetStream();

                while (!ct.IsCancellationRequested && client.Connected)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    }
                    catch (OperationCanceledException) { break; }

                    if (bytesRead == 0) break; // Client disconnected

                    messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    // Process all complete JSON messages in the buffer.
                    // Messages are separated by newlines OR we try to parse the full buffered string.
                    await ProcessMessageBuffer(messageBuffer, stream, ct);
                }
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Logger.LogError("Client connection error", ex);
            }
            finally
            {
                lock (_clients) { _clients.Remove(client); }
                try { client.Close(); } catch { }
                Logger.Log("MCP Server disconnected");
            }
        }

        /// <summary>
        /// Handle message framing. The buffer may contain:
        /// - Incomplete JSON (wait for more data)
        /// - Exactly one JSON message
        /// - Multiple JSON messages concatenated
        /// 
        /// Strategy: Use brace-counting to find message boundaries reliably,
        /// then parse the extracted substring. This avoids the re-serialization
        /// mismatch and ensures no data is lost between messages.
        /// </summary>
        private async Task ProcessMessageBuffer(StringBuilder messageBuffer, NetworkStream stream, CancellationToken ct)
        {
            while (messageBuffer.Length > 0)
            {
                var data = messageBuffer.ToString().TrimStart();
                if (string.IsNullOrEmpty(data))
                {
                    messageBuffer.Clear();
                    break;
                }

                // Use brace-counting to find the end of the first complete JSON object
                var endIdx = FindJsonObjectEnd(data);
                if (endIdx <= 0) break; // Incomplete JSON — wait for more data

                // Extract the complete JSON message and update the buffer
                var jsonStr = data.Substring(0, endIdx);
                var remaining = data.Substring(endIdx);
                messageBuffer.Clear();
                if (remaining.Length > 0)
                    messageBuffer.Append(remaining);

                try
                {
                    var request = JObject.Parse(jsonStr);

                    // Process the request
                    var response = await ProcessRequest(request);
                    var responseStr = JsonConvert.SerializeObject(response) + "\n";
                    var responseBytes = Encoding.UTF8.GetBytes(responseStr);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length, ct);
                }
                catch (JsonReaderException)
                {
                    // Malformed JSON — skip this message and continue
                    Logger.LogError($"Malformed JSON message skipped: {jsonStr.Substring(0, Math.Min(100, jsonStr.Length))}...");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing message", ex);
                    break;
                }
            }
        }

        /// <summary>
        /// Find the end index of a complete JSON object by counting braces.
        /// Returns the index AFTER the closing brace, or -1 if incomplete.
        /// </summary>
        private static int FindJsonObjectEnd(string data)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < data.Length; i++)
            {
                char c = data[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i + 1;
                }
            }

            return -1; // Incomplete
        }

        private async Task<JObject> ProcessRequest(JObject request)
        {
            var id = request["id"]?.ToString() ?? "0";
            var method = request["method"]?.ToString() ?? "";
            var parameters = request["params"] as JObject ?? new JObject();

            try
            {
                Logger.Log($"Executing command: {method}");

                // Execute through external event manager (thread-safe Revit API access)
                var result = await _eventManager.ExecuteCommandAsync(method, parameters);

                return new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["result"] = result
                };
            }
            catch (Exception ex)
            {
                Logger.LogError($"Command '{method}' failed", ex);
                return new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["error"] = new JObject
                    {
                        ["code"] = -32000,
                        ["message"] = ex.InnerException?.Message ?? ex.Message
                    }
                };
            }
        }
    }
}
