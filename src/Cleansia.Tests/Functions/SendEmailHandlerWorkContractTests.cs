using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Tests.Infrastructure;
using Cleansia.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The cleaner's copy of the contract for work: the discriminator routes the acceptance to an e-mail to
/// the cleaner's own address in their language with the contract PDF attached, read under the order's
/// company; a redelivery sends once; an acceptance that is not there sends nothing.
/// </summary>
public sealed class SendEmailHandlerWorkContractTests
{
    private const string TenantId = "cleansia-cz";
    private const string EmployeeId = "01HZX9N6M7Q8R9S0T1V2W3EMPL";
    private static readonly byte[] ContractPdf = [0x25, 0x50, 0x44, 0x46];

    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IContractConfirmationService> _confirmations = new();
    private readonly Mock<IWorkContractAcceptanceRepository> _acceptances = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly InMemoryIdempotencyGuard _guard = new();

    private SendEmailHandler CreateHandler() => new(
        _emailService.Object,
        _guard,
        _tenantProvider.Object,
        Mock.Of<IPromoCodeRepository>(),
        Mock.Of<ITenantRepository>(),
        Mock.Of<ICompanyInfoRepository>(),
        NullLogger<SendEmailHandler>.Instance,
        Mock.Of<IOrderRepository>(),
        TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
        Mock.Of<IUnitOfWork>(),
        Mock.Of<ICancellationPolicyResolver>(),
        Mock.Of<IReceivableRepository>(),
        _confirmations.Object,
        _acceptances.Object,
        _employees.Object);

    private (WorkContractAcceptance Acceptance, Employee Cleaner) Arrange()
    {
        var facts = new WorkContractFacts(
            "ORD-7Q2K", new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), 180, 450m, "CZK", "Praha 2 · 120 xx", "cz", 2, 1, [], [], []);
        var text = WorkContractTestData.Document().Texts.Single(t => t.Id == WorkContractTestData.TextIdCs);
        var acceptance = WorkContractAcceptance.Create(
            "01HZX9N6M7Q8R9S0T1V2W3ORDR", "01HZX9N6M7Q8R9S0T1V2W3SEAT", EmployeeId, text, WorkContractTestData.Version,
            "cleansia.partner", null, null, null, facts.ToJson());
        acceptance.TenantId = TenantId;

        var cleaner = Employee.CreateWithUser(User.CreateWithGoogle("petra@example.test", "Petra", "Dvořáková", "google-1", "sk"));
        cleaner.Id = EmployeeId;

        _acceptances.Setup(r => r.GetByIdAsync(acceptance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(acceptance);
        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(cleaner);
        _confirmations
            .Setup(c => c.ForWorkContractAsync(acceptance, cleaner, "sk", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContractPdf, "work-contract-ORD-7Q2K.pdf"));
        return (acceptance, cleaner);
    }

    private static string Body(string acceptanceId) => JsonSerializer.Serialize(
        new QueueEnvelope<SendWorkContractEmailMessage>(
            MessageKeys.WorkContractEmail(acceptanceId), TenantId, new SendWorkContractEmailMessage(acceptanceId, TenantId)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task The_Cleaner_Is_Sent_The_Contract_Once_In_Their_Language_Under_The_Orders_Company()
    {
        var (acceptance, _) = Arrange();
        var handler = CreateHandler();

        await handler.HandleAsync(Body(acceptance.Id), CancellationToken.None);
        await handler.HandleAsync(Body(acceptance.Id), CancellationToken.None);

        _emailService.Verify(s => s.SendWorkContractEmailAsync(
            "petra@example.test", "Petra Dvořáková", "ORD-7Q2K", ContractPdf, "work-contract-ORD-7Q2K.pdf", "sk",
            It.IsAny<CancellationToken>()), Times.Once);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.AtLeastOnce);
    }

    [Fact]
    public async Task An_Acceptance_That_Is_Not_There_Sends_Nothing()
    {
        Arrange();

        await CreateHandler().HandleAsync(Body("01HZX9N6M7Q8R9S0T1V2W3NONE"), CancellationToken.None);

        _emailService.Verify(s => s.SendWorkContractEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class InMemoryIdempotencyGuard : IIdempotencyGuard
    {
        private readonly HashSet<string> _claimed = [];

        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(!_claimed.Add(messageKey));

        public Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(_claimed.Contains(messageKey));

        public Task MarkProcessedAsync(string messageKey, CancellationToken ct = default)
        {
            _claimed.Add(messageKey);
            return Task.CompletedTask;
        }
    }
}
