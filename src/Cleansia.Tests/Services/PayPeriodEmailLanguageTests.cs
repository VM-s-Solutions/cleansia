using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Services.Pdf;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// A cleaner's stored language preference has to reach the two mails that tell them how much money
/// they are owed — the period-closed statement and the period-end reminder. Persisting the preference
/// and then rendering in a constant is the same defect as never persisting it, one layer down, and it
/// is invisible from both ends: the save succeeds, the mail sends.
///
/// The preference is therefore NOT hand-set here. It is written by the same command a client calls
/// (<see cref="UpdateCurrentUser"/>), so what these assert is the whole path — submitted, stored,
/// read back by the producer — rather than the producer's half of it. A fixture that assigned the
/// column directly would stay green with the command's writer deleted.
/// </summary>
public class PayPeriodEmailLanguageTests
{
    private const string SignupLanguage = "en";
    private const string ChosenLanguage = "uk";
    private const string CleanerEmail = "cleaner@cleansia.test";
    private const string CleanerPhone = "+420777111222";

    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IEmployeePayoutDetailsRepository> _payoutDetailsRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICountryInvoiceConfigRepository> _countryInvoiceConfigRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<IBlobContainerClientFactory> _blobContainerClientFactory = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IPayoutReferenceAllocator> _payoutReferenceAllocator = new();

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private readonly List<string> _closedEmailLanguages = [];
    private readonly List<string> _reminderEmailLanguages = [];

    public PayPeriodEmailLanguageTests()
    {
        _payoutReferenceAllocator
            .Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestVariableSymbol));

        _emailService
            .Setup(s => s.SendPeriodClosedEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, DateOnly _, DateOnly _, DateTime _, string _,
                string languageCode, byte[]? _, string? _, CancellationToken _) =>
                _closedEmailLanguages.Add(languageCode))
            .ReturnsAsync("message-id");

        _emailService
            .Setup(s => s.SendPeriodEndReminderEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, DateOnly _, DateOnly _, int _, string _,
                string languageCode, CancellationToken _) =>
                _reminderEmailLanguages.Add(languageCode))
            .ReturnsAsync("message-id");
    }

    /// <summary>
    /// Signs a cleaner up in <see cref="SignupLanguage"/>, then — when asked — runs the real profile
    /// command to switch them. The switch has to go through the handler: that call is the only thing
    /// standing between "the client sent a language" and "the column holds it".
    /// </summary>
    private async Task<Employee> ArrangeCleanerAsync(string? switchesTo)
    {
        var user = User.CreateWithPassword(
            CleanerEmail, "Password1", "First", "Last", UserProfile.Employee, SignupLanguage);
        user.Id = "user-1";
        user.Update("First", "Last", CleanerPhone, new DateOnly(1990, 1, 1));

        if (switchesTo is not null)
        {
            _session.Setup(s => s.GetUserId()).Returns(user.Id);
            _userRepository
                .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _orderRepository
                .Setup(r => r.GetOrdersByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Order>());

            var saved = await new UpdateCurrentUser.Handler(
                    _userRepository.Object, _orderRepository.Object, _session.Object,
                    _blobContainerClientFactory.Object)
                .Handle(
                    new UpdateCurrentUser.Command(
                        Id: null,
                        FirstName: "First",
                        LastName: "Last",
                        PhoneNumber: CleanerPhone,
                        BirthDate: null,
                        Photo: null,
                        LanguageCode: switchesTo),
                    CancellationToken.None);

            Assert.True(saved.IsSuccess);
        }

        var employee = Employee.CreateWithUser(user);
        employee.Id = PayrollMockFactory.EmployeeId;

        _employeeRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { employee }.AsQueryable().BuildMock());
        _employeeRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { employee }.AsQueryable().BuildMock());

        return employee;
    }

    private PayPeriodBackgroundService CreateCloseService()
    {
        var expiredPeriod = PayrollMockFactory.OpenPeriod();
        _payPeriodRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { expiredPeriod }.AsQueryable().BuildMock());
        _payPeriodRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { PayrollMockFactory.OpenPeriod() }.AsQueryable().BuildMock());

        // No unpaid pays: the run stops before invoice/PDF generation and still sends the statement,
        // which is the leg under test.
        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OrderEmployeePay>());

        _invoiceRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<EmployeeInvoice>().AsQueryable().BuildMock());

        var currency = CurrencyMockFactory.Generate();
        currency.Id = PayrollMockFactory.CurrencyId;
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        return new PayPeriodBackgroundService(
            _payPeriodRepository.Object,
            _employeeRepository.Object,
            _emailService.Object,
            _unitOfWork.Object,
            NullLogger<PayPeriodBackgroundService>.Instance,
            _currencyRepository.Object,
            _invoiceRepository.Object,
            _payoutDetailsRepository.Object,
            _orderPayRepository.Object,
            _companyInfoRepository.Object,
            _languageRepository.Object,
            _countryInvoiceConfigRepository.Object,
            _countryConfigurationRepository.Object,
            _pdfService.Object,
            _blobContainerClientFactory.Object,
            _tenantProvider.Object,
            _payoutReferenceAllocator.Object);
    }

    private PeriodReminderBackgroundService CreateReminderService()
    {
        var endsInThreeDays = PayPeriod.CreateBiWeekly(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).AddDays(-13));
        _payPeriodRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { endsInThreeDays }.AsQueryable().BuildMock());

        return new PeriodReminderBackgroundService(
            _payPeriodRepository.Object,
            _employeeRepository.Object,
            _emailService.Object,
            NullLogger<PeriodReminderBackgroundService>.Instance);
    }

    [Fact]
    public async Task The_Period_Closed_Statement_Is_Sent_In_The_Language_The_Cleaner_Saved()
    {
        await ArrangeCleanerAsync(switchesTo: ChosenLanguage);

        await CreateCloseService().CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);

        Assert.Equal([ChosenLanguage], _closedEmailLanguages);
    }

    [Fact]
    public async Task The_Period_End_Reminder_Is_Sent_In_The_Language_The_Cleaner_Saved()
    {
        await ArrangeCleanerAsync(switchesTo: ChosenLanguage);

        await CreateReminderService().SendPeriodEndRemindersAsync(CancellationToken.None);

        Assert.Equal([ChosenLanguage], _reminderEmailLanguages);
    }

    [Fact]
    public async Task A_Cleaner_Who_Never_Changed_It_Keeps_The_Language_Signup_Stamped()
    {
        await ArrangeCleanerAsync(switchesTo: null);

        await CreateCloseService().CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);

        Assert.Equal([SignupLanguage], _closedEmailLanguages);
        Assert.Equal(Constants.Language.English, SignupLanguage);
    }
}
