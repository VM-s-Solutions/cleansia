using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The <see cref="GenerateInvoice.Handler"/> money aggregation: the created invoice sums the
/// fetched order pays exactly, every fetched pay is assigned to the new invoice so a second
/// generation can't double-bill, and a net-negative pay set clamps the total to zero.
/// Mocked repositories; no DB.
/// </summary>
public class GenerateInvoiceCommandHandlerTests
{
    private const string EmployeeId = PayrollMockFactory.EmployeeId;
    private const string PayPeriodId = PayrollMockFactory.PayPeriodId;

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<IPayoutReferenceAllocator> _payoutReferenceAllocator = new();
    private readonly Mock<ICashLedgerRepository> _cashLedger = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IBlobContainerClientFactory> _blobContainerClientFactory = new();
    private readonly Mock<IBlobContainerClient> _blobContainerClient = new();

    private const string CleanerEmail = "jana.novakova@cleansia.test";
    private static readonly byte[] Pdf = [37, 80, 68, 70];
    private readonly PayPeriod _period = PayrollMockFactory.ClosedPeriod();
    private readonly Employee _cleaner = Cleaner();
    private readonly List<(string Email, string Language, DateOnly Start, DateOnly End, byte[]? Pdf, string? FileName)> _emails = [];

    public GenerateInvoiceCommandHandlerTests()
    {
        _payPeriodRepository.Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(_period);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(() => new[] { _cleaner }.AsQueryable().BuildMock());
        _blobContainerClientFactory
            .Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices))
            .Returns(_blobContainerClient.Object);
        _blobContainerClient
            .Setup(c => c.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobFile(new MemoryStream(Pdf), "application/pdf"));
        _emailService
            .Setup(e => e.SendPeriodClosedEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<byte[]?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly, DateOnly, DateTime, string, byte[]?, string?, CancellationToken>(
                (email, _, start, end, _, language, pdf, fileName, _) => _emails.Add((email, language, start, end, pdf, fileName)))
            .ReturnsAsync("message-id");
        _payoutReferenceAllocator
            .Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestVariableSymbol));
        _payoutReferenceAllocator
            .Setup(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestInvoiceNumber));
        _mediator.Setup(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RegenerateInvoicePdf.Response("invoice.pdf")));
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(IMediator))).Returns(_mediator.Object);
        services.Setup(s => s.GetService(typeof(ITenantProvider))).Returns(new Mock<ITenantProvider>().Object);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(() =>
        {
            var scope = new Mock<IServiceScope>();
            scope.SetupGet(s => s.ServiceProvider).Returns(services.Object);
            return scope.Object;
        });
    }

    private GenerateInvoice.Handler CreateHandler() => new(
        _invoiceRepository.Object,
        _orderPayRepository.Object,
        _payoutReferenceAllocator.Object,
        _cashLedger.Object,
        _payPeriodRepository.Object,
        _employeeRepository.Object,
        _emailService.Object,
        _blobContainerClientFactory.Object,
        _scopeFactory.Object,
        NullLogger<GenerateInvoice.Handler>.Instance);

    private void ArrangeOrderPays(params OrderEmployeePay[] pays) =>
        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pays);

    [Fact]
    public async Task Cancellation_After_Financial_Commit_Stops_Document_Work()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay(currencyId: "currency-1"),
            PayrollMockFactory.OrderPay(currencyId: "currency-2"));
        _mediator.Setup(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None));

        _invoiceRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sums_Order_Pays_Into_Invoice_Exactly()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 100.50m, extrasPay: 10m, expensesPay: 5.25m, bonusPay: 20m, deductionPay: 3m),
            PayrollMockFactory.OrderPay(basePay: 200m, extrasPay: 0m, expensesPay: 12.75m, bonusPay: 0m, deductionPay: 8m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(328.50m, added!.SubTotal); // (100.50+10+5.25) + (200+0+12.75)
        Assert.Equal(20m, added.BonusAmount);
        Assert.Equal(11m, added.DeductionAmount);
        Assert.Equal(2, added.TotalOrders);
        Assert.Equal(337.50m, added.TotalAmount); // 328.50 + 20 - 11
        _invoiceRepository.Verify(r => r.Add(It.IsAny<EmployeeInvoice>()), Times.Once);
    }

    [Fact]
    public async Task Assigns_Every_Fetched_Pay_To_The_New_Invoice()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 50m),
            PayrollMockFactory.OrderPay(basePay: 75m),
            PayrollMockFactory.OrderPay(basePay: 25m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(pays, p => Assert.Equal(added!.Id, p.EmployeeInvoiceId));
    }

    [Fact]
    public async Task Clamped_Pay_Is_Invoiced_At_Persisted_TotalPay_Not_Raw_Components()
    {
        // MaximumPay clamped the first order to 400 at calculation time (its components sum to
        // 650). The invoice must bill the persisted TotalPay — the same basis the period-pays
        // preview (GrandTotal = Σ TotalPay) shows the admin and the employee.
        var pays = new[]
        {
            ClampedOrderPay(basePay: 500m, extrasPay: 100m, expensesPay: 50m, clampedTotalPay: 400m),
            PayrollMockFactory.OrderPay(basePay: 100m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(pays.Sum(p => p.TotalPay), added!.TotalAmount);
        Assert.Equal(500m, added.SubTotal);
        Assert.Equal(500m, added.TotalAmount);
    }

    [Fact]
    public async Task Net_Negative_Pay_Set_Clamps_Invoice_Total_To_Zero()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 100m, bonusPay: 0m, deductionPay: 500m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, added!.TotalAmount);
    }

    /// <summary>
    /// The invoice number is a per-company series with the same yearly cap as the variable symbol, so
    /// its refusal must be the handler's refusal: no row is staged and no pay row is assigned to a
    /// document that does not exist, or the pay would be invoiced-without-an-invoice forever.
    /// </summary>
    [Fact]
    public async Task A_Refused_Invoice_Number_Refuses_The_Command_And_Stages_Nothing()
    {
        var pays = new[] { PayrollMockFactory.OrderPay(basePay: 100m), PayrollMockFactory.OrderPay(basePay: 50m) };
        ArrangeOrderPays(pays);
        _payoutReferenceAllocator
            .Setup(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<string>(new Error(
                nameof(EmployeeInvoice.InvoiceNumber),
                BusinessErrorMessage.InvoiceReferenceCapacityExhausted)));

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(EmployeeInvoice.InvoiceNumber), result.Error!.Code);
        Assert.Equal(BusinessErrorMessage.InvoiceReferenceCapacityExhausted, result.Error.Message);
        _invoiceRepository.Verify(r => r.Add(It.IsAny<EmployeeInvoice>()), Times.Never);
        Assert.All(pays, p => Assert.Null(p.EmployeeInvoiceId));
    }

    /// <summary>
    /// Owner ruling 2026-09-28, decision 23: an invoice issued by hand sets the cash the cleaner holds in
    /// its currency off against it exactly as the close does, and leaves its own amounts alone.
    /// </summary>
    [Fact]
    public async Task The_Cash_The_Cleaner_Holds_Is_Set_Off_Against_The_Invoice_Up_To_Its_Total()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay(basePay: 400m));
        _cashLedger
            .Setup(r => r.GetHeldUnderLockAsync(EmployeeId, PayrollMockFactory.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(650m);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(i => added = i);
        CashLedgerEntry? entry = null;
        _cashLedger.Setup(r => r.Add(It.IsAny<CashLedgerEntry>())).Callback<CashLedgerEntry>(e => entry = e);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(400m, added!.TotalAmount);
        Assert.Equal(400m, added.CashSetOffAmount);
        Assert.Equal(0m, added.TransferAmount);
        Assert.NotNull(entry);
        Assert.Equal(CashLedgerEntryKind.SetOff, entry!.Kind);
        Assert.Equal(-400m, entry.Amount);
        Assert.Equal(added.InvoiceNumber, entry.Note);
    }

    [Fact]
    public async Task A_Cleaner_Who_Holds_No_Cash_Gets_No_Set_Off()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay(basePay: 400m));
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(i => added = i);

        await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.Equal(0m, added!.CashSetOffAmount);
        Assert.Equal(400m, added.TransferAmount);
        _cashLedger.Verify(r => r.Add(It.IsAny<CashLedgerEntry>()), Times.Never);
    }

    [Fact]
    public async Task A_Refused_Generation_Does_Not_Attempt_To_Render_A_Document()
    {
        ArrangeOrderPays();

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsFailure);
        _mediator.Verify(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Charge_The_Pay_Cannot_Cover_Waits_In_The_Open_Period_And_The_Pay_Is_Invoiced_In_Full()
    {
        var charge = DisputeCharge(150m);
        ArrangeOrderPays(PayrollMockFactory.OrderPay(basePay: 100m), charge);
        ArrangeOpenPeriod();
        var added = new List<EmployeeInvoice>();
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(added.Add);

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var invoice = Assert.Single(added);
        Assert.Equal((100m, 0m, 100m), (invoice.SubTotal, invoice.DeductionAmount, invoice.TotalAmount));
        _payoutReferenceAllocator.Verify(a => a.AllocateAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal((OpenPeriodId, (string?)null), (charge.PayPeriodId, charge.EmployeeInvoiceId));
    }

    [Fact]
    public async Task A_Charge_In_One_Currency_Leaves_The_Invoice_In_Another_Alone()
    {
        var charge = DisputeCharge(150m, currencyId: "currency-czk");
        ArrangeOrderPays(PayrollMockFactory.OrderPay(basePay: 100m, currencyId: "currency-eur"), charge);
        ArrangeOpenPeriod();
        var added = new List<EmployeeInvoice>();
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(added.Add);

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var invoice = Assert.Single(added);
        Assert.Equal("currency-eur", invoice.CurrencyId);
        Assert.Equal((100m, 0m, 100m), (invoice.SubTotal, invoice.DeductionAmount, invoice.TotalAmount));
        Assert.Equal((OpenPeriodId, (string?)null), (charge.PayPeriodId, charge.EmployeeInvoiceId));
    }

    [Fact]
    public async Task A_Charge_Alone_Issues_No_Invoice_Claims_No_Reference_And_Waits_In_The_Open_Period()
    {
        var charge = DisputeCharge(150m);
        ArrangeOrderPays(charge);
        ArrangeOpenPeriod();

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.InvoiceIds);
        _invoiceRepository.Verify(r => r.Add(It.IsAny<EmployeeInvoice>()), Times.Never);
        _payoutReferenceAllocator.Verify(a => a.AllocateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _payoutReferenceAllocator.Verify(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal((OpenPeriodId, (string?)null), (charge.PayPeriodId, charge.EmployeeInvoiceId));
        Assert.Empty(_emails);
    }

    /// <summary>
    /// The charge comes off first and the cash second: 800 of pay less a 150 charge is 650, all of it set off
    /// against the 1 000 held, nothing to transfer, and the cleaner still holds 350.
    /// </summary>
    [Fact]
    public async Task A_Carried_Charge_Comes_Off_Before_The_Cash_Is_Set_Off()
    {
        var charge = DisputeCharge(150m);
        ArrangeOrderPays(PayrollMockFactory.OrderPay(basePay: 800m), charge);
        _cashLedger
            .Setup(r => r.GetHeldUnderLockAsync(EmployeeId, PayrollMockFactory.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(i => added = i);
        CashLedgerEntry? setOff = null;
        _cashLedger.Setup(r => r.Add(It.IsAny<CashLedgerEntry>())).Callback<CashLedgerEntry>(e => setOff = e);

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal((800m, 150m, 650m), (added!.SubTotal, added.DeductionAmount, added.TotalAmount));
        Assert.Equal((650m, 0m), (added.CashSetOffAmount, added.TransferAmount));
        Assert.Equal(-650m, setOff!.Amount);
        Assert.Equal(added.Id, charge.EmployeeInvoiceId);
    }

    // ── The invoice e-mail, under the night run's rules ──────────────

    [Fact]
    public async Task Each_Issued_Invoice_Is_Emailed_Once_With_Its_Pdf_In_The_Cleaners_Language()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay(currencyId: "currency-1"), PayrollMockFactory.OrderPay(currencyId: "currency-2"));
        _payoutReferenceAllocator
            .Setup(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BusinessResult.Success(PayrollMockFactory.NextTestInvoiceNumber()));
        var added = new List<EmployeeInvoice>();
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(added.Add);

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, added.Count);
        Assert.Equal(added.Select(i => $"{i.InvoiceNumber}.pdf").Order(), _emails.Select(e => e.FileName!).Order());
        Assert.All(_emails, e =>
        {
            Assert.Equal((CleanerEmail, "cs", _period.StartDate, _period.EndDate), (e.Email, e.Language, e.Start, e.End));
            Assert.Equal(Pdf, e.Pdf);
        });
    }

    [Fact]
    public async Task Only_A_Cleaner_With_An_Active_Account_And_An_Email_Is_Emailed()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay());
        await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);
        Assert.Single(_emails);

        _emails.Clear();
        _cleaner.User!.IsActive = false;
        ArrangeOrderPays(PayrollMockFactory.OrderPay());
        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_emails);
    }

    [Theory]
    [InlineData(false, CleanerEmail)]
    [InlineData(true, "")]
    public async Task An_Inactive_Cleaner_Or_One_Without_An_Email_Is_Not_Emailed(bool cleanerActive, string email)
    {
        _cleaner.IsActive = cleanerActive;
        typeof(User).GetProperty(nameof(User.Email))!.SetValue(_cleaner.User, email);
        ArrangeOrderPays(PayrollMockFactory.OrderPay());

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_emails);
    }

    [Fact]
    public async Task An_Invoice_Whose_Pdf_Did_Not_Render_Is_Still_Announced_Without_An_Attachment()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay());
        _mediator.Setup(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RegenerateInvoicePdf.Response>(
                new Error(nameof(RegenerateInvoicePdf.Command.InvoiceId), BusinessErrorMessage.PdfGenerationFailed)));

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(_emails);
        Assert.Equal((CleanerEmail, (string?)null), (sent.Email, sent.FileName));
        Assert.Null(sent.Pdf);
        _blobContainerClient.Verify(c => c.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Failed_Email_Leaves_The_Invoice_Issued()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay());
        _emailService
            .Setup(e => e.SendPeriodClosedEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<byte[]?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SendGrid is unavailable"));

        var result = await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _invoiceRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(e => e.SendPeriodClosedEmailAsync(
            CleanerEmail, It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<DateTime>(), It.IsAny<string>(),
            It.IsAny<byte[]?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private const string OpenPeriodId = "period-open";

    private void ArrangeOpenPeriod()
    {
        var open = PayrollMockFactory.OpenPeriod(new DateOnly(2026, 1, 15));
        open.Id = OpenPeriodId;
        _payPeriodRepository.Setup(r => r.GetActivePeriodAsync(It.IsAny<CancellationToken>())).ReturnsAsync(open);
    }

    private static OrderEmployeePay DisputeCharge(decimal amount, string currencyId = PayrollMockFactory.CurrencyId)
    {
        var job = PayrollMockFactory.OrderPay(basePay: 600m, payPeriodId: "period-earlier", currencyId: currencyId);
        job.AssignToInvoice("invoice-earlier");
        return job.CarryDisputeCharge(PayPeriodId, "dispute-1", amount, "Bathroom left uncleaned");
    }

    private static Employee Cleaner()
    {
        var user = User.CreateWithPassword(CleanerEmail, "Password1", "Jana", "Nováková", UserProfile.Employee, languageCode: "cs");
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        return employee;
    }

    // PayrollMockFactory derives TotalPay from the components, so a pay where the min/max clamp
    // fired (TotalPay < components sum) has to be created directly.
    private static OrderEmployeePay ClampedOrderPay(
        decimal basePay, decimal extrasPay, decimal expensesPay, decimal clampedTotalPay) =>
        OrderEmployeePay.Create(
            orderId: $"order-{Guid.NewGuid():N}",
            employeeId: EmployeeId,
            payPeriodId: PayPeriodId,
            currencyId: PayrollMockFactory.CurrencyId,
            basePay: basePay,
            extrasPay: extrasPay,
            expensesPay: expensesPay,
            totalPay: clampedTotalPay);
}
