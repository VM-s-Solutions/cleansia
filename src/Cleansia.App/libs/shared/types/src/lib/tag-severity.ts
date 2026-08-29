/**
 * The severity tokens PrimeNG's `p-tag` accepts.
 *
 * PrimeNG 20 narrowed `[severity]` from `string` to this union, which broke eleven bindings at once —
 * all of them fed by three functions that returned `string`. Those three now return this type, so the
 * compiler checks the value where it is PRODUCED rather than at every template that consumes it.
 *
 * Declared here rather than imported from PrimeNG because PrimeNG does not export it: the union is
 * written inline in `p-tag`'s own declaration, so there is nothing to re-export. Keep it in step with
 * `node_modules/primeng/tag` if a future major adds a token.
 */
export type TagSeverity =
  | 'success'
  | 'secondary'
  | 'info'
  | 'warn'
  | 'danger'
  | 'contrast'
  | undefined
  | null;
