#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using DayOfWeek = Cleansia.Core.Domain.Enums.DayOfWeek;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdateEmployee
{
    public class Validator : BaseUserValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly ITaxIdValidator _taxIdValidator;

        public Validator(
            ICountryRepository countryRepository,
            IEmployeeRepository employeeRepository,
            IUserSessionProvider userSessionProvider,
            ITaxIdValidator taxIdValidator)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));
            _taxIdValidator = taxIdValidator ?? throw new ArgumentNullException(nameof(taxIdValidator));

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
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId)
                // Employees can live anywhere within a serviced country —
                // commuters from a non-serviced city are allowed, but the
                // country itself must be one we operate in.
                .MustAsync(countryRepository.IsServicedAsync)
                .WithMessage(BusinessErrorMessage.CountryNotServiced);

            RuleFor(c => c.Phone)
                .ValidatePhoneNumber();

            AddEmailRules(c => c.Email);

            RuleFor(c => c.PassportId)
                .ValidatePassportId();

            RuleFor(c => c.RegistrationNumber)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(c => c.RegistrationNumber)
                .MustAsync(async (command, value, ct) =>
                {
                    var result = await _taxIdValidator.ValidateRegistrationNumberAsync(
                        command.CountryId, command.EntityType, value, ct);
                    return result.IsValid;
                })
                .WithMessage("validation.registration_number.invalid_format");

            RuleFor(c => c.VatNumber)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(c => !string.IsNullOrWhiteSpace(c.VatNumber));

            RuleFor(c => c.VatNumber)
                .MustAsync(async (command, value, ct) =>
                {
                    var result = await _taxIdValidator.ValidateVatNumberAsync(
                        command.CountryId, value, ct);
                    return result.IsValid;
                })
                .WithMessage("validation.vat_number.invalid_format")
                .When(c => !string.IsNullOrWhiteSpace(c.VatNumber));

            RuleFor(c => c.LegalEntityName)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(200)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(c => c.EntityType == EmployeeEntityType.LegalEntity);

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

            RuleFor(c => c.Availability)
                .Must(BeValidAvailability)
                .WithMessage(BusinessErrorMessage.InvalidAvailabilityFormat)
                .When(c => c.Availability?.Any() == true);
        }

        private bool BeValidAvailability(Dictionary<string, List<TimeRangeDto>>? availability)
        {
            if (availability == null || !availability.Any())
            {
                return true;
            }

            var validDays = Enum.GetNames(typeof(DayOfWeek));

            foreach (var (key, timeRanges) in availability)
            {
                // Key must be either a valid day name or a valid date (yyyy-MM-dd)
                if (!validDays.Contains(key) && !DateOnly.TryParseExact(key, "yyyy-MM-dd", out _))
                    return false;

                foreach (var timeRange in timeRanges)
                {
                    if (!TimeSpan.TryParse(timeRange.Start, out var start) ||
                        !TimeSpan.TryParse(timeRange.End, out var end))
                    {
                        return false;
                    }

                    if (start >= end)
                    {
                        return false;
                    }
                }
            }

            return true;
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
        string? State,
        string NationalityId,
        string Phone,
        string Email,
        string PassportId,
        EmployeeEntityType EntityType,
        string RegistrationNumber,
        string? VatNumber,
        string? LegalEntityName,
        string Iban,
        string? EmergencyName,
        string? EmergencyPhone,
        bool Consent,
        List<BlobFileDto>? Documents = null,
        Dictionary<string, List<TimeRangeDto>>? Availability = null) : ICommand<Response>;

    public record TimeRangeDto(string Start, string End);

    public record Response(string EmployeeId);

    internal class Handler(
        IEmployeeRepository employeeRepository,
        IEmployeeDocumentRepository employeeDocumentRepository,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory clientFactory,
        IAddressGeocoder addressGeocoder) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);
            var address = CreateOrUpdateAddress(employee!, command);
            await addressGeocoder.PopulateCoordinatesAsync(address, cancellationToken);

            await UploadDocuments(employee!, command, cancellationToken);
            var availability = ConvertAvailability(command.Availability);

            UpdateEmployeeDetails(employee!, command, address, availability);

            return BusinessResult.Success(new Response(employee!.Id));
        }

        private static Address CreateOrUpdateAddress(Employee employee, Command command)
        {
            return employee.Address is not null
                ? employee.Address.Update(command.Street, command.City, command.ZipCode, command.CountryId, command.State)
                : Address.Create(command.Street, command.City, command.ZipCode, command.CountryId, command.State);
        }

        private async Task UploadDocuments(Employee employee, Command command, CancellationToken cancellationToken)
        {
            if (command.Documents?.Any() != true)
            {
                return;
            }

            var client = clientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
            var employeeDocumentsPath = string.Format(Constants.VirtualDirectories.EmployeeDocuments, employee.Id);
            var currentUser = userSessionProvider.GetUserEmail() ?? "system";

            foreach (var document in command.Documents)
            {
                if (string.IsNullOrWhiteSpace(document.Base64Content))
                {
                    continue;
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{document.FileName}";
                var fullFilePath = $"{employeeDocumentsPath}/{uniqueFileName}";
                var contentType = document.ContentType ?? "application/octet-stream";

                await using var stream = new MemoryStream(Convert.FromBase64String(document.Base64Content.ExtractBase64Data()));
                var fileSizeBytes = stream.Length;

                var metadata = MetadataExtensions.CreateDocumentMetadata(
                    document.FileName ?? "unknown",
                    contentType,
                    employee.UserId);

                await client.UploadAsync(fullFilePath, stream, metadata, cancellationToken);

                var employeeDocument = EmployeeDocument.Create(
                    employee.Id,
                    document.FileName ?? uniqueFileName,
                    fullFilePath,
                    contentType,
                    fileSizeBytes,
                    DocumentType.Other,
                    null,
                    currentUser);

                employeeDocumentRepository.Add(employeeDocument);
            }
        }

        private static Dictionary<string, List<TimeRange>> ConvertAvailability(Dictionary<string, List<TimeRangeDto>>? availabilityDto)
        {
            if (availabilityDto == null || !availabilityDto.Any())
                return new Dictionary<string, List<TimeRange>>();

            var availability = new Dictionary<string, List<TimeRange>>();

            foreach (var (day, timeRanges) in availabilityDto)
            {
                var domainTimeRanges = timeRanges
                    .Select(dto => new TimeRange
                    {
                        Start = TimeSpan.Parse(dto.Start),
                        End = TimeSpan.Parse(dto.End)
                    })
                    .ToList();

                availability[day] = domainTimeRanges;
            }

            return availability;
        }

        private static void UpdateEmployeeDetails(Employee employee, Command command, Address address, Dictionary<string, List<TimeRange>> availability)
        {
            employee.User!.Update(
                command.FirstName,
                command.LastName,
                command.Phone,
                command.BirthDate);

            employee.UpdateEmployeeDetails(
                command.EntityType,
                command.RegistrationNumber,
                command.VatNumber,
                command.LegalEntityName,
                command.NationalityId,
                command.PassportId,
                command.Iban,
                address,
                availability,
                command.EmergencyName,
                command.EmergencyPhone);
        }
    }
}