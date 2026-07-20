#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using DayOfWeek = Cleansia.Core.Domain.Enums.DayOfWeek;

namespace Cleansia.Core.AppServices.Features.Employees;

[AuditAction("employee.availability.update", ResourceType = "User")]
public class AdminUpdateEmployeeAvailability
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IEmployeeRepository employeeRepository)
        {
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
    }

    public record Command(
        string EmployeeId,
        Dictionary<string, List<TimeRangeDto>>? Availability) : ICommand<Response>;

    public record TimeRangeDto(string Start, string End);

    public record Request(Dictionary<string, List<TimeRangeDto>>? Availability);

    public record Response(string EmployeeId);

    // Ids only — the schedule values are the subject's working-hours pattern (personal data the audit
    // log must not copy). The row records that this admin changed this user's availability, keyed on
    // the USER id the employee drill-in filters on.
    public record AvailabilitySnapshot(string UserId, string EmployeeId);

    public class Handler(
        IEmployeeRepository employeeRepository,
        IAuditContext auditContext) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            var availability = ConvertAvailability(command.Availability);
            employee!.UpdateAvailability(availability);

            var snapshot = new AvailabilitySnapshot(employee.UserId, employee.Id);
            auditContext.RecordChange("User", employee.UserId, snapshot, snapshot);

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
