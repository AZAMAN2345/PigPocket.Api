import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { gzipSync } from 'node:zlib';
import { createHash } from 'node:crypto';

const root = fileURLToPath(new URL('../dist/', import.meta.url));
const port = Number(process.env.PORT || 8082);
const cache = new Map();
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css', '.json': 'application/json', '.png': 'image/png', '.ico': 'image/x-icon', '.ttf': 'font/ttf', '.woff2': 'font/woff2', '.svg': 'image/svg+xml' };

await stat(resolve(root, 'index.html')).catch(() => {
  console.error('Build the website first: npm run build:web');
  process.exit(1);
});

createServer(async (request, response) => {
  try {
    if (!['GET', 'HEAD'].includes(request.method)) {
      response.writeHead(405, { Allow: 'GET, HEAD' }); response.end(); return;
    }
    const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    let path = resolve(root, '.' + pathname);
    if (path !== resolve(root) && !path.startsWith(resolve(root) + sep)) {
      response.writeHead(403); response.end(); return;
    }
    if (!extname(path)) path = resolve(root, 'index.html');
    let asset = cache.get(path);
    if (!asset) {
      const bytes = await readFile(path);
      const extension = extname(path);
      asset = {
        bytes, gzip: /\.(html|js|css|json|svg|ttf)$/.test(extension) ? gzipSync(bytes) : null,
        type: types[extension] || 'application/octet-stream',
        etag: '"' + createHash('sha256').update(bytes).digest('hex').slice(0, 24) + '"',
      };
      cache.set(path, asset);
    }
    const compressed = asset.gzip && /\bgzip\b/.test(request.headers['accept-encoding'] || '');
    const body = compressed ? asset.gzip : asset.bytes;
    const headers = {
      'Content-Type': asset.type,
      'Cache-Control': /[.-][a-f0-9]{16,}\./.test(path) ? 'public, max-age=31536000, immutable' : 'no-cache',
      'ETag': asset.etag, 'Vary': 'Accept-Encoding', 'X-Content-Type-Options': 'nosniff',
      ...(compressed ? { 'Content-Encoding': 'gzip' } : {}),
    };
    if (request.headers['if-none-match'] === asset.etag) {
      response.writeHead(304, headers); response.end(); return;
    }
    response.writeHead(200, { ...headers, 'Content-Length': body.length });
    response.end(request.method === 'HEAD' ? undefined : body);
  } catch (error) {
    response.writeHead(error.code === 'ENOENT' ? 404 : 400);
    response.end('Resource unavailable');
  }
}).listen(port, '127.0.0.1', () => console.log(`Piggy Pockets: http://localhost:${port} (production preview)`));
