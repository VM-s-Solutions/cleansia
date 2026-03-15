import { AngularNodeAppEngine, createNodeRequestHandler, isMainModule, writeResponseToNodeResponse } from '@angular/ssr/node';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createServer } from 'node:http';

const serverDistFolder = dirname(fileURLToPath(import.meta.url));
const browserDistFolder = resolve(serverDistFolder, '../browser');

const angularApp = new AngularNodeAppEngine();

const server = createServer(async (req, res) => {
  // Serve static files from the browser dist folder
  const url = req.url ?? '/';

  // Try Angular SSR first
  const angularResponse = await angularApp.handle(req);
  if (angularResponse) {
    await writeResponseToNodeResponse(angularResponse, res);
    return;
  }

  // Fallback for unmatched routes
  res.statusCode = 404;
  res.end('Not Found');
});

if (isMainModule(import.meta.url)) {
  const port = parseInt(process.env['PORT'] ?? '4000', 10);
  server.listen(port, () => {
    console.log(`Node server listening on http://localhost:${port}`);
  });
}

export default createNodeRequestHandler(async (req, res) => {
  const angularResponse = await angularApp.handle(req);
  if (angularResponse) {
    await writeResponseToNodeResponse(angularResponse, res);
  }
});
