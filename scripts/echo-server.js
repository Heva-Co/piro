#!/usr/bin/env node
/**
 * Minimal HTTP server for testing Piro checks: echoes the request and always returns 200.
 *
 * Usage:
 *   node scripts/echo-server.js [port]
 *
 * Default port: 9999
 * Stop it (Ctrl+C / kill the process) to simulate a down check.
 */
const http = require("node:http");

const port = Number(process.argv[2]) || 9999;

const server = http.createServer((req, res) => {
  let body = [];
  req.on("data", (chunk) => body.push(chunk));
  req.on("end", () => {
    body = Buffer.concat(body).toString("utf-8");

    console.log(`[echo-server] ${req.socket.remoteAddress} - ${req.method} ${req.url}`);

    const headerLines = Object.entries(req.headers).map(([k, v]) => `${k}: ${v}`);
    const responseText = [
      `${req.method} ${req.url} HTTP/${req.httpVersion}`,
      ...headerLines,
      "",
      body,
    ].join("\n");

    res.writeHead(200, { "Content-Type": "text/plain; charset=utf-8" });
    res.end(responseText);
  });
});

server.listen(port, () => {
  console.log(`Echo server listening on http://localhost:${port} (Ctrl+C to stop / simulate a down check)`);
});
