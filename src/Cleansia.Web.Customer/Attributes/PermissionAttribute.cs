using Cleansia.Core.AppServices.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Cleansia.Web.Customer.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PermissionAttribute(string permission) : AuthorizeAttribute(policy: permission.ToPhysicalPolicy());
