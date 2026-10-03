import { PackageListItem, PackageServiceSummary } from '../client/customer-client';

/**
 * A package and a service it already includes, booked together, book that service twice: it is done
 * twice and charged twice, and the platform does not merge the two. So a form that picks services
 * and packages marks such a service and asks before either half of the pair goes in; it never
 * removes one. Lives here because the booking wizard and the recurring form must give the same
 * answer. → /product/business-rules#charging-a-package-and-a-service-together
 */

/** Each service the chosen packages include, with the chosen packages that include it. */
export function chosenPackagesByService(
  packages: readonly PackageListItem[],
  chosenPackageIds: readonly string[],
): ReadonlyMap<string, readonly PackageListItem[]> {
  const chosen = new Set(chosenPackageIds);
  const byService = new Map<string, PackageListItem[]>();
  for (const pkg of packages) {
    if (!pkg.id || !chosen.has(pkg.id)) continue;
    for (const included of pkg.includedServices ?? []) {
      if (!included.serviceId) continue;
      const including = byService.get(included.serviceId) ?? [];
      if (!including.includes(pkg)) including.push(pkg);
      byService.set(included.serviceId, including);
    }
  }
  return byService;
}

/** The services a package includes that are already chosen on their own. */
export function includedServicesAlreadyChosen(
  pkg: PackageListItem | undefined,
  chosenServiceIds: readonly string[],
): PackageServiceSummary[] {
  return (pkg?.includedServices ?? []).filter(
    (included) => !!included.serviceId && chosenServiceIds.includes(included.serviceId),
  );
}
