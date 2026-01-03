using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class GetDocumentVersionHistory
{
    public class Query : IQuery<Response>
    {
        public string DocumentId { get; init; } = default!;
    }

    public class Response
    {
        public List<EmployeeDocumentItem> Versions { get; init; } = [];
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.DocumentId)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(IEmployeeDocumentRepository documentRepository)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Query.DocumentId),
                    BusinessErrorMessage.NotFound));
            }

            var versions = await documentRepository.GetVersionHistoryAsync(request.DocumentId, cancellationToken);

            var versionItems = versions
                .Select(v => v.MapToDto())
                .ToList();

            return BusinessResult.Success(new Response
            {
                Versions = versionItems
            });
        }
    }
}
