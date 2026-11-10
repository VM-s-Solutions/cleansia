import { AdminClient, ExtraListItem, PagedDataOfExtraListItem } from '@cleansia/admin-services';
import { of, throwError } from 'rxjs';
import { extraLabel, loadExtraNames } from './extra-names';

const clientReturning = (result: unknown) =>
  ({ adminExtraClient: { getPaged: jest.fn().mockReturnValue(result) } }) as unknown as AdminClient;

describe('extra names', () => {
  it('names a known slug and falls back to the slug for an unknown one', () => {
    const page = PagedDataOfExtraListItem.fromJS({
      data: [ExtraListItem.fromJS({ slug: 'inside-oven', name: 'Inside oven' })],
    });
    let names = {};
    loadExtraNames(clientReturning(of(page))).subscribe((n) => (names = n));

    expect(extraLabel('inside-oven', names)).toBe('Inside oven');
    expect(extraLabel('new-extra', names)).toBe('new-extra');
  });

  it('falls back to slugs when the catalogue fails to load', () => {
    let names: Record<string, string> | undefined;
    loadExtraNames(clientReturning(throwError(() => new Error('x')))).subscribe((n) => (names = n));

    expect(names).toEqual({});
  });
});
