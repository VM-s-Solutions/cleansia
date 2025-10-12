#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdateEmployee
{
    public class Validator : BaseUserValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            ICountryRepository countryRepository,
            IEmployeeRepository employeeRepository,
            IUserSessionProvider userSessionProvider)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));

            RuleFor(c => c)
                .MustAsync(AllowedToUpdateEmployee)
                .WithMessage(BusinessErrorMessage.NotAllowedToUpdateEmployee);

            RuleFor(c => c.EmployeeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound);

            AddFirstNameRules(c => c.FirstName);
            AddLastNameRules(c => c.LastName);

            RuleFor(c => c.BirthDate)
                .Cascade(CascadeMode.Stop)
                .MustBeValidDate()
                .MustBeInPast()
                .MustBeReasonableAge();

            RuleFor(c => c.Street)
                .ValidateStreetAddress();

            RuleFor(c => c.City)
                .ValidateCity();

            RuleFor(c => c.ZipCode)
                .ValidateZipCode();

            RuleFor(c => c.NationalityId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId);

            RuleFor(c => c.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId);

            RuleFor(c => c.Phone)
                .ValidatePhoneNumber();

            AddEmailRules(c => c.Email);

            RuleFor(c => c.PassportId)
                .ValidatePassportId();

            RuleFor(c => c.TaxId)
                .ValidateTaxId()
                .When(c => !string.IsNullOrWhiteSpace(c.TaxId));

            RuleFor(c => c.Iban)
                .ValidateIban();

            RuleFor(c => c.EmergencyName)
                .ValidateEmergencyName()
                .When(c => !string.IsNullOrWhiteSpace(c.EmergencyName));

            RuleFor(c => c.Consent)
                .Equal(true)
                .WithMessage(BusinessErrorMessage.Required);

            RuleForEach(c => c.Documents)
                .SetValidator(new FileValidator()!)
                .When(command => command.Documents?.Any() == true);
        }

        private async Task<bool> AllowedToUpdateEmployee(Command command, CancellationToken cancellationToken)
        {
            var currentUserEmail = _userSessionProvider.GetUserEmail();
            var employee = await _employeeRepository.GetByUserEmailAsync(currentUserEmail ?? string.Empty, cancellationToken);
            return employee?.Id == command.EmployeeId;
        }

    }

    public record Command(
        string EmployeeId,
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        string Street,
        string City,
        string ZipCode,
        string CountryId,
        string NationalityId,
        string Phone,
        string Email,
        string PassportId,
        string? TaxId,
        string Iban,
        string? EmergencyName,
        string? EmergencyPhone,
        bool Consent,
        List<BlobFileDto>? Documents = null) : ICommand<Response>;

    public record Response(string EmployeeId);

    internal class Handler(
        IEmployeeRepository employeeRepository,
        IBlobContainerClientFactory clientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            var address = CreateOrUpdateAddress(employee!, command);
            var uploadedFileNames = await UploadDocuments(employee!, command, cancellationToken);

            UpdateEmployeeDetails(employee!, command, address, uploadedFileNames);

            return BusinessResult.Success(new Response(employee!.Id));
        }

        private static Address CreateOrUpdateAddress(Employee employee, Command command)
        {
            return employee.Address is not null
                ? employee.Address.Update(command.Street, command.City, command.ZipCode, command.CountryId)
                : Address.Create(command.Street, command.City, command.ZipCode, command.CountryId);
        }

        private async Task<List<string>> UploadDocuments(Employee employee, Command command, CancellationToken cancellationToken)
        {
            var uploadedFileNames = new List<string>();

            if (command.Documents?.Any() != true)
            {
                return uploadedFileNames;
            }

            var client = clientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
            var employeeDocumentsPath = string.Format(Constants.VirtualDirectories.EmployeeDocuments, employee.Id);

            foreach (var document in command.Documents)
            {
                if (string.IsNullOrWhiteSpace(document.Base64Content))
                {
                    continue;
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{document.FileName}";
                var fullFileName = $"{employeeDocumentsPath}/{uniqueFileName}";

                await using var stream = new MemoryStream(Convert.FromBase64String(document.Base64Content.ExtractBase64Data()));

                var metadata = MetadataExtensions.CreateDocumentMetadata(
                    document.FileName ?? "unknown",
                    document.ContentType ?? "application/octet-stream",
                    employee.UserId);

                await client.UploadAsync(fullFileName, stream, metadata, cancellationToken);
                uploadedFileNames.Add(uniqueFileName);
            }

            return uploadedFileNames;
        }

        private static void UpdateEmployeeDetails(Employee employee, Command command, Address address, List<string> uploadedFileNames)
        {
            employee.User!.Update(
                command.FirstName,
                command.LastName,
                command.Phone,
                command.BirthDate);

            employee.UpdateEmployeeDetails(
                command.TaxId ?? string.Empty,
                address,
                new Dictionary<string, List<TimeRange>>());

            if (uploadedFileNames.Any())
            {
                employee.AddDocumentFileNames(uploadedFileNames);
            }
        }
    }
}