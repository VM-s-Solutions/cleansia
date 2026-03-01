using Cleansia.Config.Abstractions;
using MediatR;

namespace Cleansia.Web.Mobile.Abstractions;

public abstract class MobileApiController(IMediator mediator) : CleansiaApiController(mediator);
