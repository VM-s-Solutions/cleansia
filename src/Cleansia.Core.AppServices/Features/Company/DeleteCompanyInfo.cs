using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Company;

public class DeleteCompanyInfo
{
    public record Command(string CompanyInfoId) : ICommand<Response>;

    public record Response(bool Success);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICompanyInfoRepository companyInfoRepository)
        {
            RuleFor(x => x.CompanyInfoId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(companyInfoRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.CompanyInfoNotFound);
        }
    }

    internal class Handler(ICompanyInfoRepository companyInfoRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var companyInfo = await companyInfoRepository.GetByIdAsync(command.CompanyInfoId, cancellationToken);

            if (companyInfo is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.CompanyInfoId), BusinessErrorMessage.CompanyInfoNotFound));
            }

            // Prevent deleting if this is the last active company info
            if (companyInfo.IsActive)
            {
                var activeCount = await companyInfoRepository.CountActiveAsync(cancellationToken);
                if (activeCount <= 1)
                {
                    return BusinessResult.Failure<Response>(
                        new Error(nameof(command.CompanyInfoId), BusinessErrorMessage.CompanyInfoInUse));
                }
            }

            companyInfoRepository.Remove(companyInfo);

            return BusinessResult.Success(new Response(true));
        }
    }
}