import { randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

// The run folder: one directory per run id under the project's gitignored .run/, holding the run's password and
// everything one stage hands to the next. `.run/current` names the run the later stages continue.
const ROOT = join(__dirname, '..', '..', '.run');

export interface RunOrder {
  id: string;
  slot: string;
  total: number;
  payment: 'cash' | 'card';
  guestToken?: string;
  number?: string;
  taken?: boolean;
  completed?: boolean;
  cancelled?: boolean;
  /** Phase 2: who booked it, who works it, when it was created (the oops window runs from then), the work contract text taken, the dirtiness level. */
  customer?: Role;
  crew?: Role[];
  createdAt?: string;
  textId?: string;
  level?: number;
}

export interface RunState {
  runId: string;
  password: string;
  countryId?: string;
  addressId?: string;
  employeeId?: string;
  employee2Id?: string;
  webAddressId?: string;
  customerUserId?: string;
  cashHeldAfterExecute?: number;
  closedPeriodId?: string;
  closedAt?: string;
  orders: Record<string, RunOrder>;
  /** Phase 2. `ids`: user, employee and admin-user ids by role; `flags`: cross-file gates, each the ISO time it was set. */
  ids?: Record<string, string>;
  flags?: Record<string, string>;
  serviceId?: string;
  payConfigId?: string;
  periodAtStart?: { id: string; startDate: string; endDate: string };
  day2?: string;
  invoices?: Record<string, string>;
  receivables?: Record<string, string>;
}

/** Every account a run can hold, e2e.<run>.<role>@example.com. */
export type Role =
  | 'customer' | 'webcustomer' | 'cleaner' | 'cleaner2' | 'guest'
  | 'applicant' | 'cleaner3' | 'first' | 'nocard' | 'debtor' | 'cashcap' | 'manager' | 'support' | 'accountant' | 'nobody'
  | 'iosreg1' | 'iosreg2' | 'androidreg1' | 'androidreg2';

export const runDir = (runId: string) => join(ROOT, runId);

export function newRun(now = new Date()): RunState {
  const runId = now.toISOString().replace(/[-:T]/g, '').slice(0, 12);
  // shortcut: one password per run, kept only in the gitignored run folder; the suffix meets the password policy.
  const state: RunState = { runId, password: `${randomBytes(12).toString('base64url')}Aa1!`, orders: {} };
  mkdirSync(runDir(runId), { recursive: true });
  writeFileSync(join(ROOT, 'current'), runId);
  saveRun(state);
  return state;
}

/** The run named by E2E_RUN_ID, else the one `.run/current` names; undefined when there is none. */
export function findRun(): RunState | undefined {
  const pointer = join(ROOT, 'current');
  const runId = process.env['E2E_RUN_ID'] || (existsSync(pointer) ? readFileSync(pointer, 'utf8').trim() : '');
  return runId ? findRunById(runId) : undefined;
}

export function loadRun(): RunState {
  const state = findRun();
  if (!state) throw new Error('No run: run the book stage first, or set E2E_RUN_ID to an existing run under .run/');
  return state;
}

export function saveRun(state: RunState) {
  // Flags are only ever added: keep one another file of the same invocation set after this state was loaded.
  const file = join(runDir(state.runId), 'state.json');
  const onDisk = existsSync(file) ? (JSON.parse(readFileSync(file, 'utf8')) as RunState).flags : undefined;
  if (onDisk) state.flags = { ...onDisk, ...state.flags };
  writeFileSync(file, JSON.stringify(state, null, 2));
}

/** When a cross-file gate was set (ISO time), read from disk so a gate another file set is seen; undefined if not yet. */
export const flag = (state: RunState, name: string): string | undefined =>
  (findRunById(state.runId)?.flags ?? state.flags ?? {})[name];

export function setFlag(state: RunState, name: string, at = new Date()) {
  state.flags = { ...state.flags, [name]: at.toISOString() };
  saveRun(state);
}

/** E2E_SCRATCH=1: a build lane's validation run, which skips every test that would create a role account or the throwaway service, press Close now, run the publish SQL, edit a pay period or wait for a night run. */
export const scratch = () => process.env['E2E_SCRATCH'] === '1';

function findRunById(runId: string): RunState | undefined {
  const file = join(runDir(runId), 'state.json');
  return existsSync(file) ? (JSON.parse(readFileSync(file, 'utf8')) as RunState) : undefined;
}

export const emailOf = (state: RunState, role: Role) => `e2e.${state.runId}.${role}@example.com`;
