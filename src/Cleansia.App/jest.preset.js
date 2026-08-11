const { join } = require('path');
const nxPreset = require('@nx/jest/preset').default;

module.exports = {
  ...nxPreset,
  setupFiles: [
    ...(nxPreset.setupFiles ?? []),
    join(__dirname, 'jest.polyfills.ts'),
  ],
};
