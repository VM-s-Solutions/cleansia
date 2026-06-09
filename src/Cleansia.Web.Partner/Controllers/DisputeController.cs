using Cleansia.Config.Filters;
using Cleansia.Web.Partner.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

// SEC-DSP-07: the Partner host owns NO dispute actions. The admin-policied Resolve/UpdateStatus moved
// to the Admin host (AdminDisputeController); the customer-policied Create/GetById/GetPaged/AddMessage
// live on the Customer / Mobile.Customer hosts. No cleaner files, views, resolves, or messages a
// dispute on the Partner host. The controller is intentionally empty (no routes mounted here).
[Route("api/[controller]")]
[ApiController]
[RequireCompleteProfile]
public class DisputeController(IMediator mediator) : ApiController(mediator);
