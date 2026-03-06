using Cleansia.Config.Abstractions;
using MediatR;

namespace Cleansia.Web.Customer.Abstractions;

public abstract class CustomerApiController(IMediator mediator) : CleansiaApiController(mediator);
