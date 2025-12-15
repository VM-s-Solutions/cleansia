using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public class PeriodReminderBackgroundService : IPeriodReminderBackgroundService
{
    private readonly IPayPeriodRepository _payPeriodRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<PeriodReminderBackgroundService> _logger;

    public PeriodReminderBackgroundService(
        IPayPeriodRepository payPeriodRepository,
        IEmployeeRepository employeeRepository,
        IEmailService emailService,
        ILogger<PeriodReminderBackgroundService> logger)
    {
        _payPeriodRepository = payPeriodRepository;
        _employeeRepository = employeeRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendPeriodEndRemindersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting period end reminder job at {Time}", DateTime.UtcNow);

            // Check for periods ending in 3 days
            await SendRemindersForPeriodsEndingInAsync(3, cancellationToken);

            // Check for periods ending in 1 day
            await SendRemindersForPeriodsEndingInAsync(1, cancellationToken);

            _logger.LogInformation("Period end reminder job completed successfully at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending period end reminders");
            throw;
        }
    }

    private async Task SendRemindersForPeriodsEndingInAsync(int daysRemaining, CancellationToken cancellationToken)
    {
        var upcomingPeriods = await _payPeriodRepository
            .GetActivePeriodsEndingInDaysAsync(daysRemaining, cancellationToken);

        if (!upcomingPeriods.Any())
        {
            _logger.LogInformation("No periods ending in {Days} days", daysRemaining);
            return;
        }

        _logger.LogInformation("Found {Count} period(s) ending in {Days} days", upcomingPeriods.Count, daysRemaining);

        foreach (var period in upcomingPeriods)
        {
            await SendRemindersForPeriodAsync(period, daysRemaining, cancellationToken);
        }
    }

    private async Task SendRemindersForPeriodAsync(
        Domain.EmployeePayroll.PayPeriod period,
        int daysRemaining,
        CancellationToken cancellationToken)
    {
        var activeEmployees = await _employeeRepository.GetAllActiveWithUserAsync(cancellationToken);

        _logger.LogInformation(
            "Sending reminders for period {PeriodLabel} ending on {EndDate} to {EmployeeCount} employees",
            period.GetPeriodLabel(),
            period.EndDate,
            activeEmployees.Count);

        foreach (var employee in activeEmployees)
        {
            try
            {
                if (employee.User == null || string.IsNullOrWhiteSpace(employee.User.Email))
                {
                    _logger.LogWarning("Employee {EmployeeId} has no user or email, skipping reminder", employee.Id);
                    continue;
                }

                var employeeName = $"{employee.User.FirstName} {employee.User.LastName}";
                var languageCode = employee.User.PreferredLanguageCode ?? "en";

                await _emailService.SendPeriodEndReminderEmailAsync(
                    employee.User.Email,
                    employeeName,
                    period.StartDate,
                    period.EndDate,
                    daysRemaining,
                    period.GetPeriodLabel(),
                    languageCode,
                    cancellationToken);

                _logger.LogInformation(
                    "Sent period end reminder ({Days} days) to employee {EmployeeId} ({Email})",
                    daysRemaining,
                    employee.Id,
                    employee.User.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send period end reminder to employee {EmployeeId}",
                    employee.Id);
                // Continue with other employees even if one fails
            }
        }
    }
}
