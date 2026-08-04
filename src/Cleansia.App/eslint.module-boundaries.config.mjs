/**
 * The ONE `@nx/enforce-module-boundaries` constraint table for the workspace.
 *
 * It lives in its own module because it has to be spread from two places — the root
 * `eslint.config.mjs` (which lints every project WITHOUT a local config) and `eslint.base.config.mjs`
 * (which every project WITH a local config spreads). Those two carried separate copies, and the base
 * one had degenerated to `sourceTag:'*' -> onlyDependOnLibsWithTags:['*']`, i.e. allow everything. The
 * effect was that a cross-app client import inside a feature lib — the exact thing the scope tags
 * exist to stop — was invisible, while the same import in one of the few libs with no local config
 * was an error.
 *
 * Tag scheme (each `project.json` carries one `scope:*` and one `type:*`):
 *   scope:customer | scope:partner | scope:admin  each app's feature libs, its generated `*-services`
 *                                                 client lib and its `*-stores` data lib
 *   scope:shared                                  cross-app libs (components, directives, pipes,
 *                                                 services, models, utils, assets)
 *   type:feature | type:ui | type:data | type:util
 */
export const DEP_CONSTRAINTS = [
  {
    sourceTag: 'scope:shared',
    onlyDependOnLibsWithTags: ['scope:shared'],
  },
  {
    sourceTag: 'scope:cleansia',
    onlyDependOnLibsWithTags: ['scope:shared', 'scope:cleansia'],
  },
  {
    sourceTag: 'scope:customer',
    onlyDependOnLibsWithTags: ['scope:customer', 'scope:shared'],
  },
  {
    sourceTag: 'scope:partner',
    onlyDependOnLibsWithTags: ['scope:partner', 'scope:shared'],
  },
  {
    sourceTag: 'scope:admin',
    onlyDependOnLibsWithTags: ['scope:admin', 'scope:shared'],
  },
  {
    sourceTag: 'type:feature',
    onlyDependOnLibsWithTags: ['type:feature', 'type:ui', 'type:data', 'type:util'],
  },
  {
    sourceTag: 'type:data',
    onlyDependOnLibsWithTags: ['type:data', 'type:util'],
  },
  {
    sourceTag: 'type:ui',
    onlyDependOnLibsWithTags: ['type:ui', 'type:util'],
  },
];

/** Flat-config blocks that turn the table on. Spread into a config's exported array. */
export function moduleBoundariesRules() {
  return [
    {
      files: ['**/*.ts', '**/*.tsx', '**/*.js', '**/*.jsx'],
      rules: {
        '@nx/enforce-module-boundaries': [
          'error',
          {
            enforceBuildableLibDependency: true,
            allow: ['^.*/eslint(\\.[\\w-]+)*\\.config\\.[cm]?[jt]s$'],
            depConstraints: DEP_CONSTRAINTS,
          },
        ],
      },
    },
  ];
}
