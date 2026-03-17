import { AngularNodeAppEngine, createNodeRequestHandler, isMainModule, writeResponseToNodeResponse } from '@angular/ssr/node';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import express from 'express';

const serverDistFolder = dirname(fileURLToPath(import.meta.url));
const browserDistFolder = resolve(serverDistFolder, '../browser');

const app = express();

/**
 * Lazy-initialize AngularNodeAppEngine so the manifest is registered
 * by the Angular build wrapper before we access it.
 */
let angularApp: AngularNodeAppEngine | undefined;
function getAngularApp(): AngularNodeAppEngine {
  return (angularApp ??= new AngularNodeAppEngine());
}

app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

app.use('{*path}', (req, res, next) => {
  getAngularApp()
    .handle(req)
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
