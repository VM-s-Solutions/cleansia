import { OrderItem } from '@cleansia/customer-services';

/**
 * A score a customer gives to ONE item of an order.
 *
 * <p>The identity is the same `(serviceId, packageId?)` pair a dispute line and a refund line use, so
 * all three name an order's items the same way. A standalone service leaves `packageId` null; a
 * service that came inside a bundle carries both, because the same service can appear twice on one
 * order and an admin acting on one must not act on the other.
 * -> SubmitOrderReview.ReviewLineScore</p>
 */
export interface ReviewLineScore {
  serviceId: string;
  packageId: string | null;
  rating: number;
}

/** A scorable row, with the label the customer reads and a key the template can track by. */
export interface ReviewLineOption {
  key: string;
  serviceId: string;
  packageId: string | null;
  label: string;
  /** The bundle it came in, so two rows with the same name are distinguishable. */
  packageLabel: string | null;
}

/** Stable within one order: the server's own identity, flattened. */
export function reviewLineKey(serviceId: string, packageId: string | null): string {
  return `${packageId ?? ''}|${serviceId}`;
}

/**
 * Every item on the order the customer can score: the services bought on their own, and the services
 * inside each package.
 *
 * <p>Reads `includedServiceItems` rather than `includedServices` — the latter is a list of NAMES and
 * cannot be sent back to the server. Empty for an order whose items did not load, and the section
 * hides itself rather than showing an empty list.</p>
 */
export function buildReviewLineOptions(
  order: OrderItem | null,
  translateName: (
    name: string | undefined,
    translations: { [key: string]: { name?: string } } | undefined,
  ) => string,
): ReviewLineOption[] {
  if (!order) return [];

  const options: ReviewLineOption[] = [];

  for (const service of order.selectedServices ?? []) {
    if (!service.id) continue;
    options.push({
      key: reviewLineKey(service.id, null),
      serviceId: service.id,
      packageId: null,
      label: translateName(service.name, service.translations),
      packageLabel: null,
    });
  }

  for (const pkg of order.selectedPackages ?? []) {
    if (!pkg.id) continue;
    const packageLabel = translateName(pkg.name, pkg.translations);
    for (const included of pkg.includedServiceItems ?? []) {
      if (!included.id) continue;
      options.push({
        key: reviewLineKey(included.id, pkg.id),
        serviceId: included.id,
        packageId: pkg.id,
        // A PackageServiceRef carries no translations of its own, so the stored name is all there is.
        label: included.name ?? '',
        packageLabel,
      });
    }
  }

  return options;
}
