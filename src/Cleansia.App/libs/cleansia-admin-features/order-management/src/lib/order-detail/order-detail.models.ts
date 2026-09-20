import {
  AssignedEmployeeDto,
  OrderItem,
  TimelineEntryDto,
  TimelineSource,
  WorkContractAcceptanceDto,
} from '@cleansia/admin-services';

/** The timeline's page ceiling (GetActionTimeline refuses a larger limit). */
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

export interface CrewEntry {
  employee: AssignedEmployeeDto;
  acceptance: WorkContractAcceptanceDto | null;
}

/**
 * An acceptance names its seat, not its cleaner, and carries no name: a re-take is a new seat with
 * a new row, and a cleaner an admin re-added sits on a seat with none. The crew entry is where the
 * name is, so the pairing is by seat id.
 */
export function buildCrewEntries(order: OrderItem | null): CrewEntry[] {
  const acceptances = order?.workContractAcceptances ?? [];
  return (order?.assignedEmployees ?? []).map((employee) => ({
    employee,
    acceptance: acceptances.find((a) => !!a.orderEmployeeId && a.orderEmployeeId === employee.id) ?? null,
  }));
}
