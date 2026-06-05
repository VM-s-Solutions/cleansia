using System.Text.Json;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0002 D5 step 1 — the seam smoke test. This is the RED that proves the
/// extraction: it cannot compile until <c>Cleansia.Functions.Core</c> exists, exposes a public
/// <c>GenerateReceiptHandler</c>, and <c>Cleansia.Tests</c> references the library. It constructs
/// the consumer handler with mocked deps and invokes its body once — proving the queue-consumer
/// logic is now reachable and injectable from the test project (the precondition that makes
/// TC-IDEMP-0 / TC-DISPATCH-0 / etc. buildable).
///
/// It is a pure reachability/injectability smoke check, not a behavioral re-test of the receipt
/// flow: it drives the early-return branch (invalid OrderId), asserting the body executed without
/// throwing and short-circuited before any repository call.
/// </summary>
public class GenerateReceiptHandlerSmokeTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IReceiptService> _receiptService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    private GenerateReceiptHandler CreateHandler() => new(
        _orderRepository.Object,
        _receiptService.Object,
        _emailService.Object,
        _countryConfigurationRepository.Object,
        _unitOfWork.Object,
        _tenantProvider.Object,
        NullLogger<GenerateReceiptHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Injectable_From_Tests()
    {
        var handler = CreateHandler();
        // Benign message: a syntactically valid envelope with an invalid (non-ULID) OrderId, which
        // the consumer discards on the early-return guard before touching any dependency.
        var messageText = JsonSerializer.Serialize(
            new GenerateReceiptMessage(OrderId: "not-a-ulid", LanguageCode: "en"),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // The body runs to completion without throwing — proving the seam.
        await handler.HandleAsync(messageText, CancellationToken.None);

        // Early-return branch executed: no order lookup, no receipt/email side effects.
        _orderRepository.Verify(
            r => r.GetByIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _receiptService.VerifyNoOtherCalls();
        _emailService.VerifyNoOtherCalls();
    }
}
