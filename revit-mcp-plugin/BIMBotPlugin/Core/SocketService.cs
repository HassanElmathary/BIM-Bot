using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.Core
{
    /// <summary>
    /// TCP Socket server that listens for MCP commands from the MCP server.
    /// Uses JSON-RPC 2.0 protocol for communication.
    /// 
    /// Message framing: Length-prefixed protocol.
    ///   Send:    "Content-Length: {byteCount}\n{json}"
    ///   Receive: "Content-Length: {byteCount}\n{json}" (preferred)
    ///            OR raw JSON with brace-counting (backward compat)
    /// </summary>
    public class SocketService
    {
        private TcpListener? _listener;
        private readonly int _port;
        private readonly ExternalEventManager _eventManager;
        private CancellationTokenSource? _cts;
        private readonly List<TcpClient> _clients = new List<TcpClient>();

        private const string ContentLengthHeader = "Content-Length: ";
        private const int MaxStartRetries = 5;
        private const int RetryDelayMs = 2000;

        public bool IsRunning { get; private set; }

        public SocketService(int port, ExternalEventManager eventManager)
        {
            _port = port;
            _eventManager = eventManager;
        }

        /// <summary>
        /// Start the TCP listener with automatic retry if the port is busy.
        /// Retries up to 5 times with 2-second delays. Also kills stale
        /// processes holding the port before each attempt.
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            Exception? lastException = null;

            for (int attempt = 1; attempt <= MaxStartRetries; attempt++)
            {
                try
                {
                    // Try to free the port if something stale is holding it
                    if (attempt > 1)
                    {
                        TryKillStalePortHolder(_port);
                        Thread.Sleep(RetryDelayMs);
                    }

                    _listener = new TcpListener(IPAddress.Loopback, _port);
                    _listener.Start();
                    IsRunning = true;

                    Task.Run(() => AcceptClientsWithWatchdogAsync(_cts.Token));
                    Logger.Log($"Socket service started on port {_port} (attempt {attempt})");
                    return; // Success
                }
                catch (SocketException ex)
                {
                    lastException = ex;
                    Logger.LogError($"Start attempt {attempt}/{MaxStartRetries} failed on port {_port}", ex);
                    try { _listener?.Stop(); } catch { }
                    _listener = null;
                }
            }

            IsRunning = false;
            Logger.LogError($"All {MaxStartRetries} start attempts failed on port {_port}");
            throw lastException ?? new SocketException((int)SocketError.AddressAlreadyInUse);
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

        /// <summary>
        /// Clean restart: stop then start with full retry logic.
        /// </summary>
        public void Restart()
        {
            Logger.Log("Restarting socket service...");
            Stop();
            Thread.Sleep(500); // Brief pause to let the port release
            Start();
        }

        /// <summary>
        /// Kill any process holding our port. Best-effort — failures are silently ignored.
        /// </summary>
        private static void TryKillStalePortHolder(int port)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c netstat -ano | findstr \":{port}\" | findstr \"LISTENING\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                // Parse PID from netstat output (last column)
                foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5 && int.TryParse(parts[parts.Length - 1], out var pid) && pid > 0)
                    {
                        // Don't kill the current process
                        if (pid == System.Diagnostics.Process.GetCurrentProcess().Id) continue;

                        try
                        {
                            var stale = System.Diagnostics.Process.GetProcessById(pid);
                            Logger.Log($"Killing stale process {pid} ({stale.ProcessName}) holding port {port}");
                            stale.Kill();
                        }
                        catch { } // Process may have already exited
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("TryKillStalePortHolder failed (non-critical)", ex);
            }
        }

        /// <summary>
        /// Watchdog wrapper: runs AcceptClientsAsync and auto-restarts the listener
        /// if it crashes unexpectedly (e.g. listener disposed, socket error).
        /// </summary>
        private async Task AcceptClientsWithWatchdogAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await AcceptClientsAsync(ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    Logger.LogError("AcceptClients crashed — watchdog will restart in 3s", ex);
                    await Task.Delay(3000, ct);

                    // Recreate the listener
                    try
                    {
                        _listener?.Stop();
                        _listener = new TcpListener(IPAddress.Loopback, _port);
                        _listener.Start();
                        Logger.Log("Watchdog: listener restarted successfully");
                    }
                    catch (Exception restartEx)
                    {
                        Logger.LogError("Watchdog: failed to restart listener", restartEx);
                        IsRunning = false;
                        return;
                    }
                }
            }
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

                    Logger.Log("BIM-Bot server connected");
                    lock (_clients) { _clients.Add(client); }
                    _ = Task.Run(() => HandleClientAsync(client, ct));
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Logger.LogError("Error accepting client", ex);
                        throw; // Let watchdog handle it
                    }
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

                    // Process all complete messages in the buffer
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
                Logger.Log("BIM-Bot server disconnected");
            }
        }

        /// <summary>
        /// Process messages using length-prefixed framing (preferred) or
        /// brace-counting fallback (backward compatibility).
        /// 
        /// Length-prefixed format: "Content-Length: {N}\n{json_payload}"
        /// Fallback: raw JSON objects delimited by brace-counting.
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

                string jsonStr;

                // ── Length-prefixed framing (preferred) ──
                if (data.StartsWith(ContentLengthHeader, StringComparison.Ordinal))
                {
                    var newlineIdx = data.IndexOf('\n');
                    if (newlineIdx < 0) break; // Incomplete header — wait for more data

                    var lengthStr = data.Substring(ContentLengthHeader.Length, newlineIdx - ContentLengthHeader.Length).Trim();
                    if (!int.TryParse(lengthStr, out var contentLength) || contentLength <= 0)
                    {
                        Logger.LogError($"Invalid Content-Length: '{lengthStr}'");
                        messageBuffer.Clear();
                        break;
                    }

                    var payloadStart = newlineIdx + 1;
                    var totalNeeded = payloadStart + contentLength;

                    // Check if we have the full payload in bytes
                    var dataBytes = Encoding.UTF8.GetBytes(data);
                    if (dataBytes.Length < totalNeeded) break; // Incomplete payload — wait for more data

                    // Extract exactly contentLength bytes of payload
                    jsonStr = Encoding.UTF8.GetString(dataBytes, payloadStart, contentLength);
                    var remaining = Encoding.UTF8.GetString(dataBytes, totalNeeded, dataBytes.Length - totalNeeded);
                    messageBuffer.Clear();
                    if (remaining.Length > 0)
                        messageBuffer.Append(remaining);
                }
                // ── Brace-counting fallback (backward compatibility) ──
                else if (data[0] == '{')
                {
                    var endIdx = FindJsonObjectEnd(data);
                    if (endIdx <= 0) break; // Incomplete JSON — wait for more data

                    jsonStr = data.Substring(0, endIdx);
                    var remaining = data.Substring(endIdx);
                    messageBuffer.Clear();
                    if (remaining.Length > 0)
                        messageBuffer.Append(remaining);
                }
                else
                {
                    // Unknown data — skip until we find '{' or 'Content-Length'
                    var nextBrace = data.IndexOf('{');
                    var nextHeader = data.IndexOf(ContentLengthHeader, StringComparison.Ordinal);
                    var nextValid = -1;
                    if (nextBrace >= 0 && nextHeader >= 0) nextValid = Math.Min(nextBrace, nextHeader);
                    else if (nextBrace >= 0) nextValid = nextBrace;
                    else if (nextHeader >= 0) nextValid = nextHeader;

                    if (nextValid > 0)
                    {
                        messageBuffer.Clear();
                        messageBuffer.Append(data.Substring(nextValid));
                        continue;
                    }

                    messageBuffer.Clear();
                    break;
                }

                try
                {
                    var request = JObject.Parse(jsonStr);

                    // Process the request
                    var response = await ProcessRequest(request);

                    // Send response using length-prefixed framing
                    var responseJson = JsonConvert.SerializeObject(response);
                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    var header = Encoding.UTF8.GetBytes($"Content-Length: {responseBytes.Length}\n");

                    await stream.WriteAsync(header, 0, header.Length, ct);
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
        /// Kept for backward compatibility with clients that don't use length-prefixed framing.
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
