using Cleansia.Config.Abstractions;
using MediatR;

namespace Cleansia.Web.Mobile.Partner.Abstractions;

public abstract class MobileApiController(IMediator mediator) : CleansiaApiController(mediator);
