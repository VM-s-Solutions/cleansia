import { nxE2EPreset } from '@nx/playwright/preset';
import { defineConfig, devices } from '@playwright/test';
import { join } from 'node:path';

// The whole platform on DEV, staged because DEV time is real (e2e/README.md, "Running").
// E2E_STAGE picks one stage; without it only the offline logic tests run, so a bare run never touches DEV.
const stage = process.env['E2E_STAGE'];
const stages = ['book', 'execute', 'settle', 'people', 'orders', 'work', 'payroll', 'assist'];
if (stage && !stages.includes(stage)) throw new Error(`E2E_STAGE must be one of ${stages.join(', ')}`);

export default defineConfig({
  ...nxE2EPreset(__filename, { testDir: './src' }),
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 10 * 60_000,
  expect: { timeout: 30_000 },
  reporter: [['list'], ['./src/support/case-reporter.ts']],
  use: { ...devices['Desktop Chrome'], trace: 'retain-on-failure', locale: 'en-US' },
  // The admin SPA, served locally: DEV's admin web is behind Microsoft sign-in, and its API admits only that web's origin.
  // The devremote configuration proxies same-origin /api to the DEV admin API. 'assist' drives no browser and skips it.
  webServer: stage && stage !== 'assist'
    ? {
        command: 'npx nx serve cleansia-admin.app --configuration=devremote',
        cwd: join(__dirname, '..', '..'),
        url: 'http://localhost:4201',
        // Never reuse: a developer's own 'nx serve cleansia-admin.app' also listens on 4201 and proxies to localhost.
        reuseExistingServer: false,
        timeout: 5 * 60_000,
      }
    : undefined,
  projects: stage
    ? [{ name: stage, testDir: `./src/${stage}` }]
    : [{ name: 'logic', testMatch: /\.logic\.spec\.ts$/ }],
});
