import { TimelineEntryDto, TimelineSource } from '@cleansia/admin-services';

/** The timeline's page ceiling; one page is the whole lookup. */
export const INCIDENT_SUBJECT_LOOKUP_LIMIT = 100;

/**
 * The order payload names no customer id — it is served to cleaners too — so the subject is read off
 * the order's own trail: a successful customer act on an order is the owner's, while a refused one may
 * be a stranger's probe and a guest booking names nobody.
 */
export function resolveIncidentSubject(
  entries: readonly TimelineEntryDto[]
): string | null {
  const own = entries.find(
    (e) => e.source === TimelineSource.Customer && e.success && Boolean(e.actorId)
  );
  return own?.actorId ?? null;
}

export function incidentFileName(userId: string, generatedAt: Date): string {
  const day = generatedAt.toISOString().slice(0, 10).replace(/-/g, '');
  return `incident-${userId}-${day}.pdf`;
}
