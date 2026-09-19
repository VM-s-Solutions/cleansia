/**
 * Frontend mirror of `Cleansia.Core.AppServices.Authentication.PhysicalPolicy`.
 * Each value names the role-gate the backend applies for a logical Policy.
 * Defense-in-depth only — the backend remains authoritative; this is the UI
 * gate that hides actions the user can't perform so they don't see-then-403.
 */
export enum PhysicalPolicy {
  Anonymous = 'Anonymous',
  Authenticated = 'Authenticated',
  CustomerOnly = 'CustomerOnly',
  EmployeeOrAdmin = 'EmployeeOrAdmin',
  AdminOnly = 'AdminOnly',
  OwnerOrElevated = 'OwnerOrElevated',
  // The administrator sets — a lattice: Administrator ⊇ Manager ⊇ Support ∪ Accountant. Each needs the
  // Administrator profile AND an administrator role inside the set; AdminOnly above is any role.
  AdministratorOnly = 'AdministratorOnly',
  ManagerOrAbove = 'ManagerOrAbove',
  SupportOrAbove = 'SupportOrAbove',
  AccountantOrAbove = 'AccountantOrAbove',
}
