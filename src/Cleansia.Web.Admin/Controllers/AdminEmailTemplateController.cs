using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmailTemplates;
using Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminEmailTemplateController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("types")]
    [Permission(Policy.CanViewEmailTemplates)]
    [ProducesResponseType(typeof(List<EmailTypeListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEmailTypes(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetEmailTypes.Query(), cancellationToken);
        return HandleResult<List<EmailTypeListItemDto>>(result);
    }

    [HttpGet("type-details/{emailType}")]
    [Permission(Policy.CanViewEmailTemplates)]
    [ProducesResponseType(typeof(EmailTypeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEmailTypeDetail(
        EmailType emailType,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetEmailTypeDetail.Query(emailType), cancellationToken);
        return HandleResult<EmailTypeDetailDto>(result);
    }

    [HttpPost("types/{emailType}/send-test")]
    [Permission(Policy.CanUpdateEmailTemplate)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(SendTestEmailByType.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendTestEmailByType(
        EmailType emailType,
        [FromBody] SendTestEmailByType.Command command,
        CancellationToken cancellationToken)
    {
        if (command.EmailType != emailType)
        {
            return BadRequest("Email type in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SendTestEmailByType.Response>(result);
    }

    [HttpGet("get-paged")]
    [Permission(Policy.CanViewEmailTemplates)]
    [ProducesResponseType(typeof(PagedData<EmailTemplateTranslationListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedEmailTemplates(
        [FromQuery] GetPagedEmailTemplates.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{emailTemplateId}")]
    [Permission(Policy.CanViewEmailTemplates)]
    [ProducesResponseType(typeof(EmailTemplateTranslationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmailTemplateById(
        string emailTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetEmailTemplateById.Query(emailTemplateId), cancellationToken);
        return HandleResult<EmailTemplateTranslationDetailDto>(result);
    }

    [HttpPut("update/{emailTemplateId}")]
    [Permission(Policy.CanUpdateEmailTemplate)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UpdateEmailTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmailTemplate(
        string emailTemplateId,
        [FromBody] UpdateEmailTemplate.Command command,
        CancellationToken cancellationToken)
    {
        if (command.EmailTemplateId != emailTemplateId)
        {
            return BadRequest("Email Template ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateEmailTemplate.Response>(result);
    }

    [HttpPost("{emailTemplateId}/send-test")]
    [Permission(Policy.CanUpdateEmailTemplate)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(SendTestEmail.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendTestEmail(
        string emailTemplateId,
        [FromBody] SendTestEmail.Command command,
        CancellationToken cancellationToken)
    {
        if (command.EmailTemplateId != emailTemplateId)
        {
            return BadRequest("Email Template ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SendTestEmail.Response>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanUpdateEmailTemplate)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(CreateEmailTemplateTranslation.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEmailTemplateTranslation(
        [FromBody] CreateEmailTemplateTranslation.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateEmailTemplateTranslation.Response>(result);
    }

    [HttpDelete("delete/{emailTemplateId}")]
    [Permission(Policy.CanUpdateEmailTemplate)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DeleteEmailTemplateTranslation.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmailTemplateTranslation(
        string emailTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new DeleteEmailTemplateTranslation.Command(emailTemplateId),
            cancellationToken);
        return HandleResult<DeleteEmailTemplateTranslation.Response>(result);
    }
}