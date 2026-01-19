using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class DeleteDocument
{
    public class Command : ICommand<Response>
    {
        public string DocumentId { get; init; } = default!;
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
        }
    }

    public class Handler(
        IEmployeeDocumentRepository documentRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userEmail = userSessionProvider.GetUserEmail();
            var user = await userRepository.GetByEmailAsync(userEmail!, cancellationToken);
            var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);

            document!.SoftDelete(user!.Id);

            return BusinessResult.Success(new Response
            {
                DocumentId = document.Id
            });
        }
    }
}
