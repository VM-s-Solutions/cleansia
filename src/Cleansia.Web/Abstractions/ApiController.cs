using Cleansia.Config.Abstractions;
using MediatR;

namespace Cleansia.Web.Abstractions;

public abstract class ApiController(IMediator mediator) : CleansiaApiController(mediator);
