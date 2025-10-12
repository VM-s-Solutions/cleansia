using Cleansia.Core.AppServices.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Cleansia.Web.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PermissionAttribute(string permission) : AuthorizeAttribute(policy: permission.ToPhysicalPolicy());