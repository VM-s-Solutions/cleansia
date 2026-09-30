using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// A cancelled invoice transfers nothing, so the cash its issue set off is the cleaner's to hold again
/// (owner ruling 2026-09-28, decision 23): the cancel enters the set-off back into the ledger.
/// </summary>
public class CancelInvoiceCashSetOffTests
{
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICashLedgerRepository> _cashLedger = new();
    private readonly List<CashLedgerEntry> _entries = [];

    public CancelInvoiceCashSetOffTests()
    {
        _session.Setup(s => s.GetUserId()).Returns("admin-1");
        _cashLedger.Setup(r => r.Add(It.IsAny<CashLedgerEntry>())).Callback<CashLedgerEntry>(_entries.Add);
    }

    [Fact]
    public async Task Cancelling_An_Invoice_That_Set_Off_Cash_Gives_The_Cash_Back_To_The_Cleaner()
    {
        var invoice = Arrange(setOff: 250m);

        var result = await Handler().Handle(new CancelInvoice.Command(invoice.Id, "Wrong period"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(_entries);
        Assert.Equal(CashLedgerEntryKind.SetOff, entry.Kind);
        Assert.Equal(PayrollMockFactory.EmployeeId, entry.EmployeeId);
        Assert.Equal(PayrollMockFactory.CurrencyId, entry.CurrencyId);
        Assert.Equal(250m, entry.Amount);
        Assert.Equal(invoice.CancelledAt, entry.OccurredAt);
        Assert.Equal(invoice.InvoiceNumber, entry.Note);
    }

    [Fact]
    public async Task Cancelling_An_Invoice_That_Set_Off_Nothing_Enters_Nothing()
    {
        var invoice = Arrange(setOff: 0m);

        var result = await Handler().Handle(new CancelInvoice.Command(invoice.Id, "Wrong period"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_entries);
    }

    private EmployeeInvoice Arrange(decimal setOff)
    {
        var invoice = PayrollMockFactory.Invoice(subTotal: 1000m);
        invoice.SetOffCash(setOff);
        _invoiceRepository.Setup(r => r.GetByIdAsync(invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        return invoice;
    }

    private CancelInvoice.Handler Handler() => new(_invoiceRepository.Object, _session.Object, _cashLedger.Object);
}
