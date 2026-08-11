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
    public class Validator : AbstractValidator<Command>
    {
        /// <summary>
        /// The same cap as <c>SaveMyDocuments</c>, which writes the same container and the same table:
        /// the per-document size bound bounds one item, and the host body limit buys thousands of small
        /// ones, each a blob upload and a row.
        /// </summary>
        private const int MaxDocumentsPerRequest = 10;

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
                .MustAsync(CallerIsAnEmployee)
                .WithMessage(BusinessErrorMessage.NotAllowedToUpdateEmployee);

            RuleFor(c => c.FirstName).ValidateFirstName();
            RuleFor(c => c.LastName).ValidateLastName();

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
                .WithMessage(BusinessErrorMessage.RegistrationNumberInvalidFormat);

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
                .WithMessage(BusinessErrorMessage.VatNumberInvalidFormat)
                .When(c => !string.IsNullOrWhiteSpace(c.VatNumber));

            RuleFor(c => c.LegalEntityName)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(200)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(c => c.EntityType == EmployeeEntityType.LegalEntity);

            RuleFor(c => c.EmergencyName)
                .ValidateEmergencyName()
                .When(c => !string.IsNullOrWhiteSpace(c.EmergencyName));

            RuleFor(c => c.Consent)
                .Equal(true)
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(c => c.Documents)
                .Must(documents => documents is null || documents.Count <= MaxDocumentsPerRequest)
                .WithMessage(BusinessErrorMessage.FileCountExceeded);

            RuleForEach(c => c.Documents)
                .Cascade(CascadeMode.Stop)
                // FluentValidation skips a child validator for a null element, so without this a
                // `[null]` entry reaches the handler and is dereferenced.
                .NotNull().WithMessage(BusinessErrorMessage.Required)
                .SetValidator(new DocumentFileValidator())
                .ChildRules(document => document.RuleFor(file => file.FileName)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                    .MaximumLength(255).WithMessage(BusinessErrorMessage.MaxLength))
                // Without this the per-item rules still sniff and decode every item of a list already
                // refused for being too long, which is the cost the count cap exists to refuse.
                .When(command => command.Documents?.Count <= MaxDocumentsPerRequest);

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

        // Not an ownership comparison — the subject is server-resolved, so there is nothing for a client
        // to get wrong. What survives is the precondition the handler dereferences.
        private async Task<bool> CallerIsAnEmployee(Command command, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByUserEmailAsync(
                _userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);
            return employee is not null;
        }
    }

    public record Command(
        // [OWN-DATA] (S1): inert. The record written is always the JWT caller's; this stays on the wire
        // only so the shipped clients keep serializing unchanged. Nullable is load-bearing — a
        // non-nullable reference member makes MVC reject an ABSENT id before MediatR is reached.
        string? EmployeeId,
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
        string PassportId,
        EmployeeEntityType EntityType,
        string RegistrationNumber,
        string? VatNumber,
        string? LegalEntityName,
        string? EmergencyName,
        string? EmergencyPhone,
        bool Consent,
        List<BlobFileDto>? Documents = null,
        Dictionary<string, List<TimeRangeDto>>? Availability = null) : ICommand<Response>;

    public record TimeRangeDto(string Start, string End);

    public record Response(string EmployeeId);

    public class Handler(
        IEmployeeRepository employeeRepository,
        IEmployeeDocumentRepository employeeDocumentRepository,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory clientFactory,
        IAddressGeocoder addressGeocoder,
        IConsentService consentService) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByUserEmailAsync(
                userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(BusinessErrorMessage.EmployeeNotFound), BusinessErrorMessage.EmployeeNotFound));
            }

            var address = CreateOrUpdateAddress(employee, command);
            await addressGeocoder.PopulateCoordinatesAsync(address, cancellationToken);

            await UploadDocuments(employee, command, cancellationToken);
            var availability = ConvertAvailability(command.Availability);

            UpdateEmployeeDetails(employee, command, address, availability);

            // The validator only gates on Consent == true; GDPR Art. 7(1) requires us to be able to
            // DEMONSTRATE the consent, so the grant is persisted on the same unit of work as the
            // profile it belongs to. Re-saving an already-consented profile is a no-op.
            await consentService.TryGrantAsync(employee.UserId, ConsentType.DataProcessing, cancellationToken);

            return BusinessResult.Success(new Response(employee.Id));
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
                var uniqueFileName = $"{Guid.NewGuid()}_{document.FileName}";
                var fullFilePath = $"{employeeDocumentsPath}/{uniqueFileName}";
                var contentType = SniffedContentType.FromContent(document.Base64Content, UploadIntake.EmployeeDocument)!;

                await using var stream = new MemoryStream(Convert.FromBase64String(document.Base64Content!.ExtractBase64Data()));
                var fileSizeBytes = stream.Length;

                var metadata = MetadataExtensions.CreateDocumentMetadata(
                    document.FileName,
                    contentType,
                    employee.UserId);

                await client.UploadAsync(fullFilePath, stream, metadata, cancellationToken);

                var employeeDocument = EmployeeDocument.Create(
                    employee.Id,
                    document.FileName,
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
                address,
                availability,
                command.EmergencyName,
                command.EmergencyPhone);
        }
    }
}