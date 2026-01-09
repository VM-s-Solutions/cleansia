using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Company;
using Cleansia.Core.AppServices.Features.Company.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminCompanyController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("get-paged")]
    [Permission(Policy.CanViewCompanyInfo)]
    [ProducesResponseType(typeof(PagedData<CompanyInfoListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedCompanyInfo(
        [FromBody] GetPagedCompanyInfo.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{companyInfoId}")]
    [Permission(Policy.CanViewCompanyInfo)]
    [ProducesResponseType(typeof(CompanyInfoDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCompanyInfoById(
        string companyInfoId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCompanyInfoById.Query(companyInfoId), cancellationToken);
        return HandleResult<CompanyInfoDetailDto>(result);
    }

    [HttpPost]
    [Permission(Policy.CanCreateCompanyInfo)]
    [ProducesResponseType(typeof(CreateCompanyInfo.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCompanyInfo(
        [FromBody] CreateCompanyInfo.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateCompanyInfo.Response>(result);
    }

    [HttpPut("{companyInfoId}")]
    [Permission(Policy.CanUpdateCompanyInfo)]
    [ProducesResponseType(typeof(UpdateCompanyInfo.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCompanyInfo(
        string companyInfoId,
        [FromBody] UpdateCompanyInfo.Command command,
        CancellationToken cancellationToken)
    {
        if (command.CompanyInfoId != companyInfoId)
        {
            return BadRequest("Company Info ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateCompanyInfo.Response>(result);
    }

    [HttpDelete("{companyInfoId}")]
    [Permission(Policy.CanDeleteCompanyInfo)]
    [ProducesResponseType(typeof(DeleteCompanyInfo.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCompanyInfo(
        string companyInfoId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteCompanyInfo.Command(companyInfoId), cancellationToken);
        return HandleResult<DeleteCompanyInfo.Response>(result);
    }

    // Legacy endpoint for backward compatibility
    [HttpGet]
    [Permission(Policy.CanViewCompanyInfo)]
    [ProducesResponseType(typeof(CompanyInfoDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCompanyInfo(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCompanyInfo.Query(), cancellationToken);
        return HandleResult<CompanyInfoDetailDto?>(result);
    }
}