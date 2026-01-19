using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class DeleteMyDocument
{
    public class Command : ICommand<Response>
    {
        public string DocumentId { get; init; } = default!;
    }

    public class Response
    {
        public bool Success { get; init; }
    }

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IEmployeeDocumentRepository _documentRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeRepository employeeRepository,
            IEmployeeDocumentRepository documentRepository) : base(userRepository, userSessionProvider)
        {
            _employeeRepository = employeeRepository;
            _documentRepository = documentRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x.DocumentId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(documentRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(BeOwnedByCurrentEmployeeAsync)
                .WithMessage(BusinessErrorMessage.EmployeeDocumentNotOwned);
        }

        private async Task<bool> BeOwnedByCurrentEmployeeAsync(string documentId, CancellationToken cancellationToken)
        {
            var userEmail = _userSessionProvider.GetUserEmail();
            var employee = await _employeeRepository.GetByUserEmailAsync(userEmail!, cancellationToken);
            if (employee is null) return false;

            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            return document?.EmployeeId == employee.Id;
        }
    }

    public class Handler(
        IEmployeeRepository employeeRepository,
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
                Success = true
            });
        }
    }
}
