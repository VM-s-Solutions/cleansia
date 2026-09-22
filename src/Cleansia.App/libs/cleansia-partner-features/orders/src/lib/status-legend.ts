import { resolveStatusBadge, StatusBadgeKind } from '@cleansia/components';

/** The help legend draws the same pill, in the same tone, as the table's badge for that status. */
export const legendBadgeClass = (kind: StatusBadgeKind, member: string): string =>
  `status-badge status-badge--${resolveStatusBadge(kind, member)?.tone ?? 'neutral'}`;
