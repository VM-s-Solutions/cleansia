import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { OrderStatus, PaymentStatus } from './order-status.models';

/**
 * The shared declaration is a fourth copy of an enum three generated clients
 * already emit. Reading them through an import would be the very scope break
 * this file exists to keep closed, so the clients are read off disk instead —
 * the same idiom the i18n and brand-asset guards use.
 */

const GENERATED_CLIENTS = [
  'libs/core/admin-services/src/lib/client/admin-client.ts',
  'libs/core/partner-services/src/lib/client/partner-client.ts',
  'libs/core/customer-services/src/lib/client/customer-client.ts',
] as const;

function findAppDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'nx.json'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the Nx workspace root (nx.json)');
}

const APP_DIR = findAppDir();

function parseGeneratedEnum(
  clientPath: string,
  enumName: string
): Record<string, number> {
  const source = readFileSync(join(APP_DIR, clientPath), 'utf8');
  const block = new RegExp(`export enum ${enumName} \\{([^}]*)\\}`).exec(source);
  if (!block) {
    throw new Error(`${enumName} not found in ${clientPath}`);
  }

  const members: Record<string, number> = {};
  for (const [, name, value] of block[1].matchAll(/(\w+)\s*=\s*(-?\d+)\s*,?/g)) {
    members[name] = Number(value);
  }
  return members;
}

function numericMembers(source: Record<string, unknown>): Record<string, number> {
  return Object.fromEntries(
    Object.entries(source).filter(
      (entry): entry is [string, number] => typeof entry[1] === 'number'
    )
  );
}

describe('shared order/payment status enums stay identical to every generated client', () => {
  const shared = {
    OrderStatus: numericMembers(OrderStatus),
    PaymentStatus: numericMembers(PaymentStatus),
  };

  it.each(GENERATED_CLIENTS)('%s declares the same OrderStatus', (client) => {
    expect(parseGeneratedEnum(client, 'OrderStatus')).toEqual(
      shared.OrderStatus
    );
  });

  it.each(GENERATED_CLIENTS)('%s declares the same PaymentStatus', (client) => {
    expect(parseGeneratedEnum(client, 'PaymentStatus')).toEqual(
      shared.PaymentStatus
    );
  });

  it('reads real generated files rather than silently matching nothing', () => {
    for (const client of GENERATED_CLIENTS) {
      expect(existsSync(join(APP_DIR, client))).toBe(true);
    }
    expect(Object.keys(shared.OrderStatus).length).toBeGreaterThan(0);
    expect(Object.keys(shared.PaymentStatus).length).toBeGreaterThan(0);
  });
});
