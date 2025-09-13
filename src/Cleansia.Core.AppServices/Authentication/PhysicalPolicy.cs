namespace Cleansia.Core.AppServices.Authentication;

public class PhysicalPolicy
{
    public const string Anonymous = "Anonymous";        // AllowAnonymous
    public const string Authenticated = "Authenticated";    // any logged-in user
    public const string EmployeeOrAdmin = "EmployeeOrAdmin";  // Employee | Admin
    public const string AdminOnly = "AdminOnly";        // Admin
    public const string OwnerOrElevated = "OwnerOrElevated";  // owner OR (Employee | Admin)
}