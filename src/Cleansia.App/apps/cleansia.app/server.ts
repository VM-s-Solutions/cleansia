import { AngularNodeAppEngine, createNodeRequestHandler, isMainModule, writeResponseToNodeResponse } from '@angular/ssr/node';
import { ɵsetAngularAppEngineManifest } from '@angular/ssr';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import express from 'express';

const serverDistFolder = dirname(fileURLToPath(import.meta.url));
const browserDistFolder = resolve(serverDistFolder, '../browser');

const app = express();

let angularApp: AngularNodeAppEngine | undefined;
let manifestLoaded = false;

async function getAngularApp(): Promise<AngularNodeAppEngine> {
  if (!manifestLoaded) {
    const manifestPath = pathToFileURL(resolve(serverDistFolder, 'angular-app-engine-manifest.mjs')).href;
    const engineManifest = await import(manifestPath);
    ɵsetAngularAppEngineManifest(engineManifest.default);
    manifestLoaded = true;
  }
  return (angularApp ??= new AngularNodeAppEngine());
}

app.get('/health', (_req, res) => {
  res.json({
    status: 'healthy',
    timestamp: new Date().toISOString(),
    uptime: process.uptime(),
    memory: process.memoryUsage(),
  });
});

app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

app.use('{*path}', (req, res, next) => {
  getAngularApp()
    .then((engine) => engine.handle(req))
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

if (isMainModule(import.meta.url)) {
  const port = parseInt(process.env['PORT'] ?? '4000', 10);
  app.listen(port, () => {
    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

export default createNodeRequestHandler(app);
