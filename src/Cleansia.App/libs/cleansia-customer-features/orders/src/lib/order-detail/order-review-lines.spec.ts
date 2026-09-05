import { OrderItem } from '@cleansia/customer-services';
import { buildReviewLineOptions, reviewLineKey } from './order-review-lines.models';

/**
 * The per-item half of a review.
 *
 * <p>The original ask: a customer should be able to say which services they were unhappy with, and
 * the same for the review. One overall number cannot carry "the oven was spotless, the bathroom was
 * skipped" — it averages the two into a 3 that describes neither.</p>
 *
 * <p>Every row is identified by the server's own `(serviceId, packageId?)` pair, the same identity a
 * dispute line and a refund line use. -> SubmitOrderReview.ReviewLineScore</p>
 */
describe('review line options', () => {
  const name = (n: string | undefined) => n ?? '';
  const plain = (n: string | undefined) => name(n);

  function order(partial: Partial<OrderItem>): OrderItem {
    return partial as OrderItem;
  }

  it('lists the services bought on their own', () => {
    const options = buildReviewLineOptions(
      order({
        selectedServices: [{ id: 'svc-oven', name: 'Oven clean' }],
        selectedPackages: [],
      } as unknown as OrderItem),
      plain,
    );

    expect(options).toHaveLength(1);
    expect(options[0]).toMatchObject({
      serviceId: 'svc-oven',
      packageId: null,
      label: 'Oven clean',
      packageLabel: null,
    });
  });

  /**
   * A package is not one scorable thing. A customer who bought a Deep Clean wants to say the windows
   * were fine and the floors were not, and the server scores at service level inside the bundle.
   */
  it('unfolds a package into its included services, each carrying the package id', () => {
    const options = buildReviewLineOptions(
      order({
        selectedServices: [],
        selectedPackages: [
          {
            id: 'pkg-deep',
            name: 'Deep Clean',
            includedServiceItems: [
              { id: 'svc-oven', name: 'Oven clean' },
              { id: 'svc-windows', name: 'Windows' },
            ],
          },
        ],
      } as unknown as OrderItem),
      plain,
    );

    expect(options.map((o) => o.serviceId)).toEqual(['svc-oven', 'svc-windows']);
    expect(options.every((o) => o.packageId === 'pkg-deep')).toBe(true);
    expect(options[0].packageLabel).toBe('Deep Clean');
  });

  /**
   * THE ONE THE PAIR EXISTS FOR. The same service bought standalone AND inside a package on one
   * order is two rows, and they must not collapse — an admin acting on one must not act on the other.
   */
  it('keeps a standalone service separate from the same service inside a package', () => {
    const options = buildReviewLineOptions(
      order({
        selectedServices: [{ id: 'svc-oven', name: 'Oven clean' }],
        selectedPackages: [
          {
            id: 'pkg-deep',
            name: 'Deep Clean',
            includedServiceItems: [{ id: 'svc-oven', name: 'Oven clean' }],
          },
        ],
      } as unknown as OrderItem),
      plain,
    );

    expect(options).toHaveLength(2);
    expect(new Set(options.map((o) => o.key)).size).toBe(2);
    expect(options[0].key).toBe(reviewLineKey('svc-oven', null));
    expect(options[1].key).toBe(reviewLineKey('svc-oven', 'pkg-deep'));
  });

  it('skips items with no id, which cannot be named back to the server', () => {
    const options = buildReviewLineOptions(
      order({
        selectedServices: [{ name: 'Nameless' }, { id: 'svc-ok', name: 'Fine' }],
        selectedPackages: [],
      } as unknown as OrderItem),
      plain,
    );

    expect(options.map((o) => o.serviceId)).toEqual(['svc-ok']);
  });

  it('answers empty for no order at all, so the section hides itself', () => {
    expect(buildReviewLineOptions(null, plain)).toEqual([]);
  });

  /**
   * Names are translated through the caller's own lookup — the component's, which reads the active
   * language. Passing it in rather than injecting TranslateService keeps this a pure function.
   */
  it('labels rows through the caller-supplied translator', () => {
    const options = buildReviewLineOptions(
      order({
        selectedServices: [
          { id: 'svc-oven', name: 'Oven clean', translations: { cs: { name: 'Čištění trouby' } } },
        ],
        selectedPackages: [],
      } as unknown as OrderItem),
      (n, translations) => translations?.['cs']?.name || name(n),
    );

    expect(options[0].label).toBe('Čištění trouby');
  });
});
