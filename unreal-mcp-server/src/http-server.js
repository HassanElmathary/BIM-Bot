/**
 * HTTP/SSE Server wrapper for BIM-Bot Unreal
 * ─────────────────────────────────────────────────────────────────────────────
 * Exposes the same MCP server over StreamableHTTP transport so remote clients
 * (like Manus AI) can connect via HTTP instead of stdio.
 *
 * Usage:
 *   node src/http-server.js            (default port 3001)
 *   MCP_HTTP_PORT=8080 node src/http-server.js
 *
 * Endpoint:  POST/GET/DELETE  http://localhost:<port>/mcp
 */

const express = require('express');
const crypto = require('crypto');
const { StreamableHTTPServerTransport } = require('@modelcontextprotocol/sdk/server/streamableHttp.js');
const { isInitializeRequest } = require('@modelcontextprotocol/sdk/types.js');
const { server, client } = require('./index.js');
const { logger } = require('./logger.js');

const log = logger.child('http-server');
const PORT = parseInt(process.env.MCP_HTTP_PORT || '3001');

const app = express();
app.use(express.json());

// ─── Session management ─────────────────────────────────────────────────────

const transports = {};

// ─── CORS (allow Manus & any remote caller) ─────────────────────────────────

app.use((req, res, next) => {
  res.header('Access-Control-Allow-Origin', '*');
  res.header('Access-Control-Allow-Methods', 'GET, POST, DELETE, OPTIONS');
  res.header('Access-Control-Allow-Headers', 'Content-Type, mcp-session-id, Last-Event-ID');
  res.header('Access-Control-Expose-Headers', 'mcp-session-id');
  if (req.method === 'OPTIONS') return res.sendStatus(204);
  next();
});

// ─── Health check ───────────────────────────────────────────────────────────

app.get('/health', (_req, res) => {
  res.json({
    status: 'ok',
    unrealConnected: client.isConnected,
    bridgeAvailable: client.isBridgeAvailable,
    activeSessions: Object.keys(transports).length,
  });
});

// ─── MCP POST (JSON-RPC requests + new sessions) ───────────────────────────

app.post('/mcp', async (req, res) => {
  const sessionId = req.headers['mcp-session-id'];

  try {
    let transport;

    if (sessionId && transports[sessionId]) {
      // Reuse existing transport
      transport = transports[sessionId];
    } else if (!sessionId && isInitializeRequest(req.body)) {
      // New initialization request — create transport
      transport = new StreamableHTTPServerTransport({
        sessionIdGenerator: () => crypto.randomUUID(),
        onsessioninitialized: (sid) => {
          log.info(`Session initialized: ${sid}`);
          transports[sid] = transport;
        },
      });

      transport.onclose = () => {
        const sid = transport.sessionId;
        if (sid && transports[sid]) {
          log.info(`Session closed: ${sid}`);
          delete transports[sid];
        }
      };

      // Connect transport to MCP server BEFORE handling the request
      await server.connect(transport);
      await transport.handleRequest(req, res, req.body);
      return;
    } else {
      res.status(400).json({
        jsonrpc: '2.0',
        error: { code: -32000, message: 'Bad Request: No valid session ID provided' },
        id: null,
      });
      return;
    }

    await transport.handleRequest(req, res, req.body);
  } catch (error) {
    log.error('POST /mcp error', { error: error.message });
    if (!res.headersSent) {
      res.status(500).json({
        jsonrpc: '2.0',
        error: { code: -32603, message: 'Internal server error' },
        id: null,
      });
    }
  }
});

// ─── MCP GET (SSE stream) ───────────────────────────────────────────────────

app.get('/mcp', async (req, res) => {
  const sessionId = req.headers['mcp-session-id'];
  if (!sessionId || !transports[sessionId]) {
    return res.status(400).send('Invalid or missing session ID');
  }
  const transport = transports[sessionId];
  await transport.handleRequest(req, res);
});

// ─── MCP DELETE (session termination) ───────────────────────────────────────

app.delete('/mcp', async (req, res) => {
  const sessionId = req.headers['mcp-session-id'];
  if (!sessionId || !transports[sessionId]) {
    return res.status(400).send('Invalid or missing session ID');
  }
  const transport = transports[sessionId];
  await transport.handleRequest(req, res);
});

// ─── Start ──────────────────────────────────────────────────────────────────

client.startHealthCheck();

client.on('connected', (info) => log.info('Connected to Unreal Engine', { info }));
client.on('disconnected', () => log.warn('Lost connection to Unreal Engine'));
client.on('reconnected', (info) => log.info('Reconnected to Unreal Engine', { info }));

app.listen(PORT, () => {
  log.info(`BIM-Bot Unreal HTTP server listening on http://localhost:${PORT}/mcp`);
  log.info('Health check at http://localhost:' + PORT + '/health');
  console.log(`\n  🚀  BIM-Bot Unreal (HTTP mode) running at http://localhost:${PORT}/mcp`);
  console.log(`  📡  Health check: http://localhost:${PORT}/health`);
  console.log(`  🔧  Unreal Remote Control: ${client.baseUrl}`);
  console.log(`  🔧  Unreal MCP Bridge: ${client.bridgeUrl}\n`);
});

// ─── Graceful shutdown ──────────────────────────────────────────────────────

process.on('SIGINT', async () => {
  log.info('Shutting down HTTP server...');
  for (const sid of Object.keys(transports)) {
    try { await transports[sid].close(); } catch (_) {}
    delete transports[sid];
  }
  client.shutdown();
  process.exit(0);
});
