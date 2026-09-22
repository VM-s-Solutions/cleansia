import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { CleansiaStatusBadgeComponent } from './cleansia-status-badge.component';
import {
  resolveStatusBadge,
  STATUS_BADGE_KINDS,
  StatusBadgeKind,
  StatusBadgeTone,
  statusMemberKey,
} from './cleansia-status-badge.models';

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

const APP_ROOT = join(findSolutionDir(), 'Cleansia.App');
const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'];
const TONES: StatusBadgeTone[] = ['neutral', 'info', 'success', 'warning', 'danger'];
const KINDS = Object.keys(STATUS_BADGE_KINDS) as StatusBadgeKind[];

/** The generated enum each numbered kind mirrors; a kind with no enum is derived on the client. */
const GENERATED_ENUM: Partial<Record<StatusBadgeKind, string[]>> = {
  order: ['OrderStatus'],
  payment: ['PaymentStatus'],
  contract: ['ContractStatus'],
  invoice: ['EmployeeInvoiceStatus'],
  dispute: ['DisputeStatus'],
  payPeriod: ['PayPeriodStatus'],
  company: ['CompanyLifecycleState'],
  referral: ['ReferralStatus'],
  document: ['DocumentStatus', 'DocumentDeletionRequestStatus'],
};

const CLIENTS = ['admin', 'partner'].map((app) =>
  readFileSync(join(APP_ROOT, `libs/core/${app}-services/src/lib/client/${app}-client.ts`), 'utf-8'),
);

function generatedMembers(enumName: string): Record<string, number> | null {
  for (const source of CLIENTS) {
    const match = source.match(new RegExp(`export enum ${enumName} \\{([^}]*)\\}`));
    if (!match) continue;
    return Object.fromEntries(
      [...match[1].matchAll(/(\w+)\s*=\s*(\d+)/g)].map(([, name, value]) => [name, Number(value)]),
    );
  }
  return null;
}

function bundle(app: string, locale: string): Record<string, unknown> {
  return JSON.parse(readFileSync(join(APP_ROOT, `apps/${app}/src/assets/i18n/${locale}.json`), 'utf-8'));
}

function lookup(tree: Record<string, unknown>, key: string): unknown {
  return key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], tree);
}

describe('status badge catalogue', () => {
  it.each(KINDS)('maps every %s status to one of the five tones', (kind) => {
    const offTone = Object.entries(STATUS_BADGE_KINDS[kind].members)
      .filter(([, member]) => !TONES.includes(member.tone))
      .map(([name]) => name);
    expect(offTone).toEqual([]);
  });

  it.each(Object.entries(GENERATED_ENUM))(
    'mirrors the generated client for the %s kind, member for member and number for number',
    (kind, enumNames) => {
      const catalogued = Object.fromEntries(
        Object.entries(STATUS_BADGE_KINDS[kind as StatusBadgeKind].members).map(([name, m]) => [name, m.value]),
      );
      for (const enumName of enumNames) {
        const generated = generatedMembers(enumName);
        expect(generated).not.toBeNull();
        expect(catalogued).toEqual(generated);
      }
    },
  );

  it('has a label in every admin locale for every status it can show', () => {
    const missing: string[] = [];
    for (const locale of LOCALES) {
      const tree = bundle('cleansia-admin.app', locale);
      for (const kind of KINDS) {
        const { labelRoot, members } = STATUS_BADGE_KINDS[kind];
        for (const name of Object.keys(members)) {
          const key = `${labelRoot}.${statusMemberKey(name)}`;
          if (typeof lookup(tree, key) !== 'string') missing.push(`${locale}: ${key}`);
        }
      }
    }
    expect(missing).toEqual([]);
  });

  it('has every member of each kind the partner bundle declares, in every partner locale', () => {
    const missing: string[] = [];
    for (const locale of LOCALES) {
      const tree = bundle('cleansia-partner.app', locale);
      for (const kind of KINDS) {
        const { labelRoot, members } = STATUS_BADGE_KINDS[kind];
        if (typeof lookup(bundle('cleansia-partner.app', 'en'), labelRoot) !== 'object') continue;
        for (const name of Object.keys(members)) {
          const key = `${labelRoot}.${statusMemberKey(name)}`;
          if (typeof lookup(tree, key) !== 'string') missing.push(`${locale}: ${key}`);
        }
      }
    }
    expect(missing).toEqual([]);
  });
});

describe('resolveStatusBadge', () => {
  it('reads the number, the name and the Code pair the wire may carry as the same status', () => {
    const expected = { tone: 'info', labelKey: 'enums.order_status.on_the_way', fallbackLabel: 'OnTheWay' };
    expect(resolveStatusBadge('order', 3)).toEqual(expected);
    expect(resolveStatusBadge('order', 'OnTheWay')).toEqual(expected);
    expect(resolveStatusBadge('order', 'on_the_way')).toEqual(expected);
    expect(resolveStatusBadge('order', { value: 3, name: 'OnTheWay' })).toEqual(expected);
    expect(resolveStatusBadge('order', { name: 'OnTheWay' })).toEqual(expected);
  });

  it('trusts the number of a Code over its name, since the number is the enum', () => {
    expect(resolveStatusBadge('order', { value: 2, name: 'New' })?.labelKey).toBe('enums.order_status.confirmed');
  });

  it('reads a boolean as active or inactive', () => {
    expect(resolveStatusBadge('active', true)?.labelKey).toBe('enums.active_status.active');
    expect(resolveStatusBadge('active', false)?.labelKey).toBe('enums.active_status.inactive');
  });

  it('renders nothing for no status at all', () => {
    expect(resolveStatusBadge('order', null)).toBeNull();
    expect(resolveStatusBadge('order', undefined)).toBeNull();
    expect(resolveStatusBadge('order', '')).toBeNull();
  });

  it('shows an uncatalogued status neutral and under its raw name rather than hiding it', () => {
    expect(resolveStatusBadge('order', 42)).toEqual({ tone: 'neutral', labelKey: '', fallbackLabel: '42' });
    expect(resolveStatusBadge('order', { value: 42, name: 'Teleported' })).toEqual({
      tone: 'neutral',
      labelKey: '',
      fallbackLabel: 'Teleported',
    });
  });
});

describe('CleansiaStatusBadgeComponent', () => {
  let fixture: ComponentFixture<CleansiaStatusBadgeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaStatusBadgeComponent, TranslateModule.forRoot()],
    }).compileComponents();
    fixture = TestBed.createComponent(CleansiaStatusBadgeComponent);
  });

  function pill(): HTMLElement | null {
    return fixture.nativeElement.querySelector('.status-badge');
  }

  it('draws the tone as a modifier on the one shared class', () => {
    fixture.componentRef.setInput('kind', 'payment');
    fixture.componentRef.setInput('value', { value: 2, name: 'Paid' });
    fixture.detectChanges();

    expect(pill()?.className).toBe('status-badge status-badge--success');
    expect(pill()?.textContent?.trim()).toBe('enums.payment_status.paid');
  });

  it('draws nothing when there is no status', () => {
    fixture.componentRef.setInput('kind', 'payment');
    fixture.detectChanges();

    expect(pill()).toBeNull();
  });
});
