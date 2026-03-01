using Cleansia.Config.Abstractions;
using MediatR;

namespace Cleansia.Web.Admin.Abstractions;

public abstract class ApiController(IMediator mediator) : CleansiaApiController(mediator);
