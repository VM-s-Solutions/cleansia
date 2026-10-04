import { PackageListItem, PackageServiceSummary } from '../client/customer-client';

/**
 * A package and a service it already includes, booked together, book that service twice: it is done
 * twice and charged twice, and the platform does not merge the two. Two chosen packages that share a
 * service do the same. So a form that picks services and packages marks such a service and asks
 * before a tap books one again; it never removes one. Lives here because the booking wizard and the
 * recurring form must give the same answer.
 * → /product/business-rules#charging-a-package-and-a-service-together
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

/**
 * The services a package includes that are already in the booking, chosen on their own or through
 * another chosen package: adding the package books each of them once more.
 */
export function includedServicesAlreadyChosen(
  pkg: PackageListItem | undefined,
  packages: readonly PackageListItem[],
  chosen: { readonly selectedServiceIds: readonly string[]; readonly selectedPackageIds: readonly string[] },
): PackageServiceSummary[] {
  const throughPackages = chosenPackagesByService(
    packages,
    chosen.selectedPackageIds.filter((id) => id !== pkg?.id),
  );
  return (pkg?.includedServices ?? []).filter(
    (included) =>
      !!included.serviceId &&
      (chosen.selectedServiceIds.includes(included.serviceId) ||
        throughPackages.has(included.serviceId)),
  );
}
