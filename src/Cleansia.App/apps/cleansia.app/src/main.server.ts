import { BootstrapContext, bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app';
import { serverConfig } from './app/app.config.server';

// The context argument is NOT optional, despite the old signature compiling without it.
//
// From @angular/ssr 19.2.27 the server bootstrap is handed a BootstrapContext that carries the
// platform for this render, and bootstrapApplication must be given it. Dropping it throws
// NG0401 "Missing Platform" on EVERY server render — which type-checks clean, builds clean, and
// only shows up when something actually renders a page. It cost an e2e run to find: the SSR dev
// server answered 500 to every request until Playwright's five-minute webServer wait expired.
const bootstrap = (context: BootstrapContext) =>
  bootstrapApplication(AppComponent, serverConfig, context);

export default bootstrap;
