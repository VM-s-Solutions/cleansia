import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import {
  ADMIN_ROLE_SETS,
  AdminRoleName,
  PhysicalPolicy,
  POLICY_MAP,
  Policy,
} from '@cleansia/services';

/**
 * The web's permission map is a hand-kept mirror of `PolicyBuilder.Map`, and the hint it drives
 * over-shows on any row the two disagree on: the server refuses, but the user saw the button. This
 * reads the backend source the way the error-contract parity spec reads the controllers, so a row
 * that moves on one side and not the other reddens the build instead of shipping a 403 behind a
 * visible action.
 */
function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const SOLUTION_DIR = findSolutionDir();
const AUTH_DIR = join(SOLUTION_DIR, 'Cleansia.Core.AppServices/Authentication');
const ADMIN_ROLE_ENUM_PATH = join(SOLUTION_DIR, 'Cleansia.Core.Domain/Enums/AdminRole.cs');

function parseBackendMap(): Map<string, string> {
  const source = readFileSync(join(AUTH_DIR, 'PolicyBuilder.cs'), 'utf8');
  const start = source.indexOf('Map = new()');
  const end = source.indexOf('};', start);
  const rows = new Map<string, string>();
  const regex = /^\s*\[Policy\.(\w+)\] = PhysicalPolicy\.(\w+),/gm;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(source.slice(start, end))) !== null) {
    rows.set(match[1], match[2]);
  }
  return rows;
}

function parseBackendPhysicalPolicies(): Set<string> {
  const source = readFileSync(join(AUTH_DIR, 'PhysicalPolicy.cs'), 'utf8');
  return new Set([...source.matchAll(/public const string (\w+) = "(\w+)";/g)].map((m) => m[2]));
}

function parseBackendRoleNames(): string[] {
  const source = readFileSync(ADMIN_ROLE_ENUM_PATH, 'utf8');
  const body = source.slice(source.indexOf('{', source.indexOf('enum AdminRole')));
  return [...body.matchAll(/^\s*(\w+)\s*=\s*\d+,/gm)].map((m) => m[1]).sort();
}

function parseBackendRoleSets(): Map<string, string[]> {
  const source = readFileSync(join(AUTH_DIR, 'AdminRoleSets.cs'), 'utf8');
  const sets = new Map<string, string[]>();
  const regex = /IReadOnlySet<AdminRole> (\w+) = new HashSet<AdminRole>\s*\{([^}]*)\}/g;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(source)) !== null) {
    sets.set(
      match[1],
      [...match[2].matchAll(/AdminRole\.(\w+)/g)].map((m) => m[1]).sort()
    );
  }
  return sets;
}

describe('POLICY_MAP mirrors PolicyBuilder.Map', () => {
  const backend = parseBackendMap();
  const frontend = POLICY_MAP as Record<string, PhysicalPolicy>;

  it('reads a map of the expected size from the backend source', () => {
    expect(backend.size).toBeGreaterThan(150);
  });

  it('carries every backend row with the same physical policy', () => {
    const drift = [...backend.entries()]
      .filter(([policy, physical]) => frontend[policy] !== physical)
      .map(([policy, physical]) => `${policy}: backend ${physical}, web ${frontend[policy] ?? 'missing'}`);

    expect(drift).toEqual([]);
  });

  it('carries no row the backend map does not', () => {
    const orphans = Object.keys(frontend).filter((policy) => !backend.has(policy));

    expect(orphans).toEqual([]);
  });

  it('names every policy by its own key', () => {
    const renamed = Object.entries(Policy).filter(([key, value]) => key !== value);

    expect(renamed).toEqual([]);
  });

  it('knows every physical policy the backend registers except the deny sentinel', () => {
    const expected = parseBackendPhysicalPolicies();
    expected.delete('Deny');

    expect(new Set(Object.values(PhysicalPolicy))).toEqual(expected);
  });

  it('spells the four administrator sets exactly as AdminRoleSets does', () => {
    const backendSets = parseBackendRoleSets();
    const frontendSets = Object.fromEntries(
      Object.entries(ADMIN_ROLE_SETS).map(([set, roles]) => [set, [...roles].sort()])
    );

    expect(Object.values(AdminRoleName).sort()).toEqual(parseBackendRoleNames());
    expect(frontendSets).toEqual(Object.fromEntries(backendSets));
  });
});
