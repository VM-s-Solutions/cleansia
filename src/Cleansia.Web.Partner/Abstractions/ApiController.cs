using Cleansia.Config.Abstractions;
using MediatR;

namespace Cleansia.Web.Partner.Abstractions;

public abstract class ApiController(IMediator mediator) : CleansiaApiController(mediator);
