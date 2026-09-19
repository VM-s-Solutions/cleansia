namespace Cleansia.Core.AppServices.Authentication;

public class PhysicalPolicy
{
    public const string Anonymous = "Anonymous";        // AllowAnonymous
    public const string Authenticated = "Authenticated";    // any logged-in user
    public const string CustomerOnly = "CustomerOnly";      // Customer only (excludes Employee & Admin)
    public const string EmployeeOrAdmin = "EmployeeOrAdmin";  // Employee | Admin
    public const string AdminOnly = "AdminOnly";        // Admin, any administrator role
    public const string OwnerOrElevated = "OwnerOrElevated";  // owner OR (Employee | Admin)
    public const string Deny = "Deny";              // always 403 — fail-closed sentinel for unmapped permissions

    // The administrator sets (a lattice: Administrator ⊇ Manager ⊇ Support ∪ Accountant). Each requires
    // the Administrator profile AND an admin_role claim inside the set; AdminOnly above requires no claim.
    public const string AdministratorOnly = "AdministratorOnly";  // { Administrator }
    public const string ManagerOrAbove = "ManagerOrAbove";        // { Administrator, Manager }
    public const string SupportOrAbove = "SupportOrAbove";        // { Administrator, Manager, Support }
    public const string AccountantOrAbove = "AccountantOrAbove";  // { Administrator, Manager, Accountant }
}