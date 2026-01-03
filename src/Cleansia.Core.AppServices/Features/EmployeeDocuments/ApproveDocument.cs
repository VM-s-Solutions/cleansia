using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class ApproveDocument
{
    public class Command : ICommand<Response>
    {
        public string DocumentId { get; init; } = default!;
        public string? Notes { get; init; }
    }

    public class Response
    {
        public string DocumentId { get; init; } = default!;
    }

    public class Validator : UserEmailValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeDocumentRepository documentRepository) : base(userRepository, userSessionProvider)
        {
            RuleFor(x => x.DocumentId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(documentRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound);

            When(x => !string.IsNullOrEmpty(x.Notes), () =>
            {
                RuleFor(x => x.Notes)
                    .MaximumLength(500).WithMessage(BusinessErrorMessage.MaxLength);
            });
        }
    }

    public class Handler(
        IEmployeeDocumentRepository documentRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail();
            var adminUser = await userRepository.GetByEmailAsync(adminEmail!, cancellationToken);
            var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);

            document!.Approve(adminUser!.Id, request.Notes);

            return BusinessResult.Success(new Response
            {
                DocumentId = document.Id
            });
        }
    }
}
