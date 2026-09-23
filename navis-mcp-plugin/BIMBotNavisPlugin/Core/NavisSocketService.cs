using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// TCP Socket server for the Navisworks Manage plugin.
    /// COPIED from Revit BIMBotPlugin.Core.SocketService (same JSON-RPC 2.0 +
    /// Content-Length framing) — Revit file is NOT modified.
    /// Differences: default port 8091, handshake file service-navis.json.
    /// </summary>
    public class NavisSocketService
    {
        private TcpListener? _listener;
        private readonly int _preferredPort;
        private int _port;
        private CancellationTokenSource? _cts;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly Func<string, JObject, JObject> _handler;

        private const string ContentLengthHeader = "Content-Length: ";
        private const int PortScanRange = 10;

        public bool IsRunning { get; private set; }
        public int Port => _port;

        public NavisSocketService(int port, Func<string, JObject, JObject> handler)
        {
            _preferredPort = port;
            _port = port;
            _handler = handler;
        }

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            Exception? last = null;
            for (int candidate = _preferredPort; candidate <= _preferredPort + PortScanRange; candidate++)
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Loopback, candidate);
                    _listener.Start();
                    _port = candidate;
                    IsRunning = true;
                    Task.Run(() => AcceptLoopAsync(_cts.Token));
                    NavisServiceEndpoint.Publish(candidate);
                    return;
                }
                catch (SocketException ex) { last = ex; try { _listener?.Stop(); } catch { } _listener = null; }
            }
            IsRunning = false;
            throw last ?? new SocketException((int)SocketError.AddressAlreadyInUse);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            lock (_clients) { foreach (var c in _clients) { try { c.Close(); } catch { } } _clients.Clear(); }
            try { _listener?.Stop(); } catch { }
            IsRunning = false;
            NavisServiceEndpoint.Clear();
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!_listener!.Pending()) { await Task.Delay(100, ct); continue; }
                    var client = await _listener.AcceptTcpClientAsync();
                    lock (_clients) { _clients.Add(client); }
                    _ = Task.Run(() => HandleClientAsync(client, ct));
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch { await Task.Delay(1000, ct); }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            var buffer = new byte[65536];
            var sb = new StringBuilder();
            try
            {
                var stream = client.GetStream();
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    int n;
                    try { n = await stream.ReadAsync(buffer, 0, buffer.Length, ct); }
                    catch (OperationCanceledException) { break; }
                    if (n == 0) break;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, n));
                    await ProcessBuffer(sb, stream, ct);
                }
            }
            catch { }
            finally { lock (_clients) { _clients.Remove(client); } try { client.Close(); } catch { } }
        }

        private async Task ProcessBuffer(StringBuilder sb, NetworkStream stream, CancellationToken ct)
        {
            while (sb.Length > 0)
            {
                var data = sb.ToString().TrimStart();
                if (string.IsNullOrEmpty(data)) { sb.Clear(); break; }
                string jsonStr;
                if (data.StartsWith(ContentLengthHeader, StringComparison.Ordinal))
                {
                    var nl = data.IndexOf('\n');
                    if (nl < 0) break;
                    var lenStr = data.Substring(ContentLengthHeader.Length, nl - ContentLengthHeader.Length).Trim();
                    if (!int.TryParse(lenStr, out var len) || len <= 0) { sb.Clear(); break; }
                    var start = nl + 1;
                    var bytes = Encoding.UTF8.GetBytes(data);
                    if (bytes.Length < start + len) break;
                    jsonStr = Encoding.UTF8.GetString(bytes, start, len);
                    sb.Clear();
                    var rest = Encoding.UTF8.GetString(bytes, start + len, bytes.Length - start - len);
                    if (rest.Length > 0) sb.Append(rest);
                }
                else if (data[0] == '{')
                {
                    int end = FindJsonEnd(data);
                    if (end <= 0) break;
                    jsonStr = data.Substring(0, end);
                    sb.Clear();
                    var rest = data.Substring(end);
                    if (rest.Length > 0) sb.Append(rest);
                }
                else { sb.Clear(); break; }

                try
                {
                    var req = JObject.Parse(jsonStr);
                    var id = req["id"]?.ToString() ?? "0";
                    var method = req["method"]?.ToString() ?? "";
                    var pars = req["params"] as JObject ?? new JObject();
                    JObject response;
                    try
                    {
                        var result = await Task.Run(() => _handler(method, pars));
                        response = new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
                    }
                    catch (Exception ex)
                    {
                        response = new JObject
                        {
                            ["jsonrpc"] = "2.0",
                            ["id"] = id,
                            ["error"] = new JObject { ["code"] = -32000, ["message"] = ex.InnerException?.Message ?? ex.Message }
                        };
                    }
                    var outJson = JsonConvert.SerializeObject(response);
                    var outBytes = Encoding.UTF8.GetBytes(outJson);
                    var header = Encoding.UTF8.GetBytes($"Content-Length: {outBytes.Length}\n");
                    await stream.WriteAsync(header, 0, header.Length, ct);
                    await stream.WriteAsync(outBytes, 0, outBytes.Length, ct);
                }
                catch { break; }
            }
        }

        private static int FindJsonEnd(string data)
        {
            int depth = 0; bool inStr = false, esc = false;
            for (int i = 0; i < data.Length; i++)
            {
                char c = data[i];
                if (esc) { esc = false; continue; }
                if (c == '\\' && inStr) { esc = true; continue; }
                if (c == '"') { inStr = !inStr; continue; }
                if (inStr) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return i + 1; }
            }
            return -1;
        }
    }
}
