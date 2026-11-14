import { nxE2EPreset } from '@nx/playwright/preset';
import { defineConfig, devices } from '@playwright/test';

// The whole platform on DEV, staged because DEV time is real (e2e/README.md, "Running").
// E2E_STAGE picks one stage; without it only the offline logic tests run, so a bare run never touches DEV.
const stage = process.env['E2E_STAGE'];
const stages = ['book', 'execute', 'settle'];
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
  projects: stage
    ? [{ name: stage, testDir: `./src/${stage}` }]
    : [{ name: 'logic', testMatch: /\.logic\.spec\.ts$/ }],
});
