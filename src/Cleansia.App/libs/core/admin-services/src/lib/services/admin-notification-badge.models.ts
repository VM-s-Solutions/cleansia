export const ADMIN_NOTIFICATION_POLL_INTERVAL_MS = 60_000;

const BADGE_CAP = 99;

/**
 * The badge is a hint, not a number to reconcile: past two digits it says "many" the way the mobile
 * bells do, and at zero it says nothing at all.
 */
export function formatUnreadBadge(count: number): string | null {
  if (!Number.isFinite(count) || count <= 0) return null;
  return count > BADGE_CAP ? `${BADGE_CAP}+` : String(Math.floor(count));
}
