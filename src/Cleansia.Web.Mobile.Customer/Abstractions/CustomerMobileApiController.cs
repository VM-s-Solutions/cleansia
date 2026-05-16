using Cleansia.Config.Abstractions;
using MediatR;

namespace Cleansia.Web.Mobile.Customer.Abstractions;

/// <summary>
/// Base for controllers on the Customer Mobile API host. Mirrors the
/// (partner) <c>MobileApiController</c> contract — same body-token,
/// no-cookie-surface shape that native Android clients expect.
/// </summary>
public abstract class CustomerMobileApiController(IMediator mediator) : CleansiaApiController(mediator);
