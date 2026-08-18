import { createServer } from "node:http";
import { readFile, stat } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";

const port = 4173;
const root = fileURLToPath(new URL("../dist/client/", import.meta.url));
const mimeTypes = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".ico": "image/x-icon",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".webmanifest": "application/manifest+json; charset=utf-8",
  ".webp": "image/webp"
};

function resolveRequest(pathname) {
  const clean = decodeURIComponent(pathname).replace(/^\/+/, "");
  const target = normalize(join(root, clean || "index.html"));
  return target.startsWith(normalize(root)) ? target : join(root, "index.html");
}

const server = createServer(async (request, response) => {
  try {
    let target = resolveRequest(new URL(request.url, "http://localhost").pathname);
    try {
      const info = await stat(target);
      if (info.isDirectory()) target = join(target, "index.html");
    } catch {
      target = join(root, "index.html");
    }
    const body = await readFile(target);
    const filename = target.replaceAll("\\", "/").split("/").pop();
    const noCache = filename === "index.html" || filename === "sw.js" || filename === "manifest.webmanifest";
    response.writeHead(200, {
      "Content-Type": mimeTypes[extname(target).toLowerCase()] || "application/octet-stream",
      "Cache-Control": noCache ? "no-cache" : "public, max-age=31536000, immutable"
    });
    response.end(body);
  } catch {
    response.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" });
    response.end("Tomatotodo failed to start.");
  }
});

server.listen(port);
