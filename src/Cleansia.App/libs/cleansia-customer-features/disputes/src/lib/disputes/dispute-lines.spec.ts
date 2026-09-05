import { disputeLineKey } from './disputes.models';

/**
 * How a customer names the parts of an order that went wrong.
 *
 * <p>The original ask: "I want it to be possible that he can select services/packages he wasn't
 * satisfied with and describe what was wrong exactly in the description". The description stays ONE
 * per dispute (owner ruling); these say WHICH.</p>
 *
 * <p>The identity is the server's own `(serviceId, packageId?)` pair. The pair matters and is not
 * ceremony: a customer can buy an oven clean on its own AND get one inside a Deep Clean package on
 * the same order, and an admin refunding one of them must not refund the other.
 * -> CreateDispute.DisputeLineSelection</p>
 */
describe('dispute line identity', () => {
  it('distinguishes a standalone service from the same service inside a package', () => {
    const standalone = disputeLineKey({ serviceId: 'svc-oven', packageId: null });
    const inPackage = disputeLineKey({ serviceId: 'svc-oven', packageId: 'pkg-deep' });

    expect(standalone).not.toEqual(inPackage);
  });

  it('gives the same item the same key every time, so a tick survives a re-render', () => {
    expect(disputeLineKey({ serviceId: 'svc-oven', packageId: 'pkg-deep' })).toEqual(
      disputeLineKey({ serviceId: 'svc-oven', packageId: 'pkg-deep' }),
    );
  });

  it('distinguishes the same service in two different packages', () => {
    const inDeep = disputeLineKey({ serviceId: 'svc-oven', packageId: 'pkg-deep' });
    const inMove = disputeLineKey({ serviceId: 'svc-oven', packageId: 'pkg-move-out' });

    expect(inDeep).not.toEqual(inMove);
  });

  /**
   * The empty package id and a literal empty string must not collide — an id is a ULID, so this is
   * unreachable through the UI, but the key format is the only thing standing between two different
   * items being treated as one and it should not depend on that.
   */
  it('separates the package segment from the service segment', () => {
    const a = disputeLineKey({ serviceId: 'b', packageId: 'a' });
    const b = disputeLineKey({ serviceId: 'ab', packageId: null });

    expect(a).not.toEqual(b);
  });
});
