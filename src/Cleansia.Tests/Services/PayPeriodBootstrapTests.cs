using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The first period a company gets is opened by whichever pay calculation finds none, and two can find none at the
/// same moment. The one-open index refuses the later insert. That refusal means the period is open only if the read
/// after it finds one; otherwise the failure is someone else's and is not swallowed.
/// </summary>
public class PayPeriodBootstrapTests
{
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Refused_Insert_Counts_As_Seeded_Only_When_An_Open_Period_Is_There(bool openedByAnother)
    {
        PayPeriod? added = null;
        _payPeriodRepository.Setup(r => r.GetQueryable()).Returns(Array.Empty<PayPeriod>().AsQueryable().BuildMock());
        _payPeriodRepository.Setup(r => r.Add(It.IsAny<PayPeriod>())).Callback<PayPeriod>(p => added = p);
        _payPeriodRepository.Setup(r => r.ExistsActivePeriodAsync(It.IsAny<CancellationToken>())).ReturnsAsync(openedByAnother);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("The open period index refused the insert", new UniqueViolation()));

        var failure = await Record.ExceptionAsync(() => CreateService().EnsureOpenPeriodAsync(CancellationToken.None));

        Assert.Equal(openedByAnother ? null : typeof(DbUpdateException), failure?.GetType());
        Assert.NotNull(added);
        _payPeriodRepository.Verify(r => r.Remove(added!), Times.Once);
    }

    private PayPeriodBackgroundService CreateService() => new(
        _payPeriodRepository.Object,
        new Mock<IEmployeeRepository>().Object,
        new Mock<IEmailService>().Object,
        _unitOfWork.Object,
        NullLogger<PayPeriodBackgroundService>.Instance,
        new Mock<ICurrencyRepository>().Object,
        new Mock<IEmployeeInvoiceRepository>().Object,
        new Mock<IEmployeePayoutDetailsRepository>().Object,
        new Mock<IOrderEmployeePayRepository>().Object,
        new Mock<ICompanyInfoRepository>().Object,
        new Mock<ILanguageRepository>().Object,
        new Mock<ICountryInvoiceConfigRepository>().Object,
        new Mock<ICountryConfigurationRepository>().Object,
        new Mock<IPdfService>().Object,
        new Mock<IBlobContainerClientFactory>().Object,
        new Mock<ITenantProvider>().Object,
        new Mock<IPayoutReferenceAllocator>().Object,
        new Mock<ITenantRepository>().Object,
        new Mock<ICashLedgerRepository>().Object,
        PayPeriodTestServices.ScopeFactory(CreateService),
        PayPeriodTestServices.ClosingConfig());

    private sealed class UniqueViolation : Exception
    {
        public string SqlState => "23505";
    }
}
