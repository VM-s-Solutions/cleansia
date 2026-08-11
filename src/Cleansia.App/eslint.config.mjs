import nx from '@nx/eslint-plugin';
import { generatedDtoLiteralRules } from './eslint.generated-dto.config.mjs';
import { moduleBoundariesRules } from './eslint.module-boundaries.config.mjs';

export default [
  ...nx.configs['flat/base'],
  ...nx.configs['flat/typescript'],
  ...nx.configs['flat/javascript'],
  {
    ignores: ['**/dist'],
  },
  ...generatedDtoLiteralRules([
    'libs/core/customer-services/**/*.ts',
    'libs/data-access/**/*.ts',
    'libs/cleansia-customer-features/**/*.ts',
    'libs/cleansia-partner-features/dashboard/**/*.ts',
  ]),
  ...moduleBoundariesRules(),
  {
    files: [
      '**/*.ts',
      '**/*.tsx',
      '**/*.cts',
      '**/*.mts',
      '**/*.js',
      '**/*.jsx',
      '**/*.cjs',
      '**/*.mjs',
    ],
    // Override or add rules here
    rules: {},
  },
];
