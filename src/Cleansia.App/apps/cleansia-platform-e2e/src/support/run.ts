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
  taken?: boolean;
  completed?: boolean;
  cancelled?: boolean;
}

export interface RunState {
  runId: string;
  password: string;
  countryId?: string;
  addressId?: string;
  employeeId?: string;
  customerUserId?: string;
  cashHeldAfterExecute?: number;
  closedPeriodId?: string;
  closedAt?: string;
  orders: Record<string, RunOrder>;
}

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
  const file = runId ? join(runDir(runId), 'state.json') : '';
  return file && existsSync(file) ? (JSON.parse(readFileSync(file, 'utf8')) as RunState) : undefined;
}

export function loadRun(): RunState {
  const state = findRun();
  if (!state) throw new Error('No run: run the book stage first, or set E2E_RUN_ID to an existing run under .run/');
  return state;
}

export function saveRun(state: RunState) {
  writeFileSync(join(runDir(state.runId), 'state.json'), JSON.stringify(state, null, 2));
}

export const emailOf = (state: RunState, role: 'customer' | 'cleaner' | 'guest') =>
  `e2e.${state.runId}.${role}@example.com`;
