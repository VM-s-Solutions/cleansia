import { workspaceRoot } from '@nx/devkit';
import { nxE2EPreset } from '@nx/playwright/preset';
import { defineConfig, devices } from '@playwright/test';

// For CI, you may want to set BASE_URL to the deployed application.
const baseURL = process.env['BASE_URL'] || 'http://localhost:4202';

/**
 * Read environment variables from file.
 * https://github.com/motdotla/dotenv
 */
// require('dotenv').config();

/**
 * See https://playwright.dev/docs/test-configuration.
 */
export default defineConfig({
  ...nxE2EPreset(__filename, { testDir: './src' }),
  /* Shared settings for all the projects below. See https://playwright.dev/docs/api/class-testoptions. */
  use: {
    baseURL,
    /* Collect trace when retrying the failed test. See https://playwright.dev/docs/trace-viewer */
    trace: 'on-first-retry',
  },
  /* Run your local dev server before starting the tests. The customer app is
     SSR — its first dev build can take a few minutes, so the boot timeout is
     generous. `reuseExistingServer` lets a warm `nx serve` be reused. */
  webServer: {
    command: 'npx nx run cleansia.app:serve',
    url: 'http://localhost:4202',
    reuseExistingServer: true,
    timeout: 300_000,
    cwd: workspaceRoot,
  },
  /* Chromium-only by design: this is a single critical-path smoke (T-0271), not
     a cross-browser matrix. One browser keeps CI fast and the boot deterministic
     (`npx playwright install --with-deps chromium`). Add firefox/webkit here if
     the suite grows into a regression pack. */
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
