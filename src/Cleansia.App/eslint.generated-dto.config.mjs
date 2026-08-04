/**
 * ADR-0031: a generated `*Command`/`*Request`/`*Dto`/`*Query` built from an object
 * literal is checked for required-key completeness, so the next NSwag regen breaks
 * every one of them at once. Construct-then-assign is not.
 *
 * The rule is opted into per scope, and a scope may only opt in once its own count
 * is zero — the opt-in list IS the ratchet. Spread `generatedDtoLiteralRules()` into
 * a cleared lib's `eslint.config.mjs`; never remove it to make a new literal compile.
 *
 * Flat-config `files` globs resolve against the directory of the config that ESLint
 * loaded, so a per-lib config passes no argument and the workspace-root config
 * passes workspace-relative globs.
 */
export function generatedDtoLiteralRules(files = ['**/*.ts']) {
  return [
    {
      files,
      ignores: ['**/client/*-client.ts'],
      rules: {
        'no-restricted-syntax': [
          'error',
          {
            selector:
              "NewExpression[callee.name=/(Command|Request|Dto|Query)$/][arguments.0.type='ObjectExpression']",
            message:
              'Build a generated DTO with construct-then-assign (`const c = new X(); c.field = value;`), never an object literal — the literal is required-key checked and breaks on the next NSwag regen (ADR-0031).',
          },
        ],
      },
    },
  ];
}
