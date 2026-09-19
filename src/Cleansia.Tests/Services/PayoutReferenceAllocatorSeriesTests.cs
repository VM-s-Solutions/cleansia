using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The two per-company series the allocator composes from one counter table: the ten-digit
/// <i>variabilní symbol</i> and the <c>INV-YYYY-NNNNNN</c> invoice number. Each is its own scope on the
/// counter, so the ordinals advance independently and an exhausted year refuses both by the same key.
/// The per-company arbitration itself is a Postgres property, pinned in the integration suite.
/// </summary>
public class PayoutReferenceAllocatorSeriesTests
{
    private readonly Mock<IPayoutReferenceCounterRepository> _counters = new();
    private readonly PayoutReferenceAllocator _allocator;

    public PayoutReferenceAllocatorSeriesTests()
    {
        _allocator = new PayoutReferenceAllocator(_counters.Object);
    }

    [Theory]
    [InlineData(2026, 1, "2026000001")]
    [InlineData(2026, 999999, "2026999999")]
    [InlineData(2031, 42, "2031000042")]
    public void A_Variable_Symbol_Is_The_Year_And_A_Six_Digit_Ordinal(int year, long ordinal, string expected)
    {
        Assert.Equal(expected, PayoutReferenceAllocator.Format(year, ordinal));
    }

    [Theory]
    [InlineData(2026, 1, "INV-2026-000001")]
    [InlineData(2026, 999999, "INV-2026-999999")]
    [InlineData(2031, 42, "INV-2031-000042")]
    public void An_Invoice_Number_Is_INV_The_Year_And_A_Six_Digit_Ordinal(int year, long ordinal, string expected)
    {
        Assert.Equal(expected, PayoutReferenceAllocator.FormatInvoiceNumber(year, ordinal));
    }

    [Fact]
    public async Task The_Variable_Symbol_Is_Drawn_From_Its_Own_Scope()
    {
        _counters
            .Setup(r => r.AllocateNextAsync(DateTime.UtcNow.Year, PayoutReferenceCounter.VariableSymbolScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var result = await _allocator.AllocateAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal($"{DateTime.UtcNow.Year:D4}000007", result.Value);
        _counters.Verify(
            r => r.AllocateNextAsync(It.IsAny<int>(), PayoutReferenceCounter.InvoiceNumberScope, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task The_Invoice_Number_Is_Drawn_From_Its_Own_Scope()
    {
        _counters
            .Setup(r => r.AllocateNextAsync(DateTime.UtcNow.Year, PayoutReferenceCounter.InvoiceNumberScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _allocator.AllocateInvoiceNumberAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal($"INV-{DateTime.UtcNow.Year:D4}-000003", result.Value);
        _counters.Verify(
            r => r.AllocateNextAsync(It.IsAny<int>(), PayoutReferenceCounter.VariableSymbolScope, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task An_Exhausted_Variable_Symbol_Year_Fails_With_Capacity_Exhausted()
    {
        _counters
            .Setup(r => r.AllocateNextAsync(It.IsAny<int>(), PayoutReferenceCounter.VariableSymbolScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        var result = await _allocator.AllocateAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(EmployeeInvoice.VariableSymbol), result.Error!.Code);
        Assert.Equal(BusinessErrorMessage.InvoiceReferenceCapacityExhausted, result.Error.Message);
    }

    [Fact]
    public async Task An_Exhausted_Invoice_Number_Year_Fails_With_Capacity_Exhausted()
    {
        _counters
            .Setup(r => r.AllocateNextAsync(It.IsAny<int>(), PayoutReferenceCounter.InvoiceNumberScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        var result = await _allocator.AllocateInvoiceNumberAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(EmployeeInvoice.InvoiceNumber), result.Error!.Code);
        Assert.Equal(BusinessErrorMessage.InvoiceReferenceCapacityExhausted, result.Error.Message);
    }
}
