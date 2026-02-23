#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using DayOfWeek = Cleansia.Core.Domain.Enums.DayOfWeek;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdateAvailability
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
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

                // Date overrides with empty time ranges = day off (valid)
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
        Dictionary<string, List<TimeRangeDto>>? Availability) : ICommand<Response>;

    public record TimeRangeDto(string Start, string End);

    public record Response(string EmployeeId);

    internal class Handler(
        IEmployeeRepository employeeRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            var availability = ConvertAvailability(command.Availability);
            employee!.UpdateAvailability(availability);

            return BusinessResult.Success(new Response(employee.Id));
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
    }
}
