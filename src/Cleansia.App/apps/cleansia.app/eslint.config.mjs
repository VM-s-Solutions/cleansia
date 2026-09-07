import nx from '@nx/eslint-plugin';
import baseConfig from '../../eslint.base.config.mjs';

export default [
  ...baseConfig,
  ...nx.configs['flat/angular'],
  ...nx.configs['flat/angular-template'],
  {
    files: ['**/*.ts'],
    rules: {
      // `cleansia`, not the generator's `app`: every component in this
      // application is <cleansia-*>, which is the convention the project guide
      // states and the prefix project.json declares. The rule was asserting a
      // naming scheme nothing in the repo has ever used.
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'cleansia',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          // Both: every component in the app is <cleansia-*>, and the ROOT
          // stays <app-root> because index.html and the SSR entry bootstrap
          // that element by name.
          prefix: ['cleansia', 'app'],
          style: 'kebab-case',
        },
      ],
    },
  },
  {
    files: ['**/*.html'],
    // Override or add rules here
    rules: {},
  },
];
