using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PromoCodes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Azure.Storage.Queues;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cleansia.Tests.Features.PromoCodes;

/// <summary>
/// Covers one-code-per-address issuance and duplicate request rejection.
/// → /flows/loyalty-and-memberships#public-promo-code-requests
/// </summary>
public class RequestPromoCodeTests
{
    private const string Email = "visitor@example.com";

    /// <summary>The buffer serialises camelCase; read it back the same way.</summary>
    private static readonly JsonSerializerOptions WireJson = new() { PropertyNameCaseInsensitive = true };

    private readonly Mock<IPromoCodeRepository> _promoCodes = new();
    private readonly IPendingDispatch _pending = new InMemoryPendingDispatch();

    private RequestPromoCode.Handler CreateHandler() => new(_promoCodes.Object, _pending);

    private static SendEmailMessage SoleMessage(IPendingDispatch pending)
    {
        var drained = Assert.Single(pending.Drain());
        var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(drained.Body, WireJson);

        Assert.NotNull(envelope);
        return envelope!.Payload;
    }

    [Fact]
    public async Task A_first_request_mints_a_code_and_queues_the_email()
    {
        _promoCodes
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCode?)null);

        var result = await CreateHandler().Handle(new RequestPromoCode.Command(Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _promoCodes.Verify(r => r.Add(It.IsAny<PromoCode>()), Times.Once);

        var message = SoleMessage(_pending);
        Assert.Equal(EmailType.PromoCode, message.EmailType);
        Assert.Equal(Email, message.Email);
        Assert.StartsWith("VITEJTE-", message.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_existing_code_rejects_the_request_without_queuing_another_email()
    {
        _promoCodes
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromoCode.CreatePercent(
                "VITEJTE-EXISTS",
                percent: RequestPromoCode.FirstOrderDiscountPercent,
                minimumOrderAmount: null,
                maxRedemptionsPerUser: 1,
                globalMaxRedemptions: 1));

        var result = await CreateHandler().Handle(new RequestPromoCode.Command(Email), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("promo.already_sent", result.Error?.Message);
        _promoCodes.Verify(r => r.Add(It.IsAny<PromoCode>()), Times.Never);
        _promoCodes.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_pending.Drain());
    }

    [Fact]
    public async Task The_email_intent_is_recorded_before_the_code_is_committed()
    {
        _promoCodes
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Single(_pending.Drain()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(new RequestPromoCode.Command(Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _promoCodes.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_concurrent_duplicate_returns_the_already_sent_error()
    {
        _promoCodes
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Duplicate promo request", new UniqueConstraintException()));

        var result = await CreateHandler().Handle(new RequestPromoCode.Command(Email), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("promo.already_sent", result.Error?.Message);
    }

    private sealed class UniqueConstraintException : Exception
    {
        public string SqlState => "23505";
    }

    [Fact]
    public async Task The_same_address_always_derives_the_same_code_whatever_its_casing()
    {
        _promoCodes
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCode?)null);

        await CreateHandler().Handle(new RequestPromoCode.Command(Email), CancellationToken.None);
        var first = SoleMessage(_pending);

        var second = new InMemoryPendingDispatch();
        await new RequestPromoCode.Handler(_promoCodes.Object, second)
            .Handle(new RequestPromoCode.Command("  VISITOR@Example.COM "), CancellationToken.None);

        Assert.Equal(first.Code, SoleMessage(second).Code);
    }

    [Fact]
    public async Task Two_addresses_derive_two_different_codes()
    {
        _promoCodes
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCode?)null);

        await CreateHandler().Handle(new RequestPromoCode.Command(Email), CancellationToken.None);

        var other = new InMemoryPendingDispatch();
        await new RequestPromoCode.Handler(_promoCodes.Object, other)
            .Handle(new RequestPromoCode.Command("someone.else@example.com"), CancellationToken.None);

        Assert.NotEqual(SoleMessage(_pending).Code, SoleMessage(other).Code);
    }

    [Fact]
    public async Task Nothing_on_the_queue_carries_the_address_outside_the_recipient_field()
    {
        // The send-email consumer logs UserId. An e-mail address there would put PII
        // into a log line for a person who never signed up. → S6
        _promoCodes
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCode?)null);

        await CreateHandler().Handle(new RequestPromoCode.Command(Email), CancellationToken.None);

        var drained = Assert.Single(_pending.Drain());
        Assert.DoesNotContain(Email, drained.MessageKey, StringComparison.OrdinalIgnoreCase);

        var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(drained.Body, WireJson);
        Assert.NotNull(envelope);
        Assert.DoesNotContain(Email, envelope!.Payload.UserId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Requesting_twice_in_one_request_buffers_one_message()
    {
        // The producer-side half of the delivery bound: the key is stable, so the
        // in-request dedup collapses a double submit before it ever reaches the queue.
        _promoCodes
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCode?)null);

        var handler = CreateHandler();
        await handler.Handle(new RequestPromoCode.Command(Email), CancellationToken.None);
        await handler.Handle(new RequestPromoCode.Command(Email), CancellationToken.None);

        Assert.Single(_pending.Drain());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-address")]
    public void A_malformed_address_is_rejected_by_the_validator(string email)
    {
        new RequestPromoCode.Validator()
            .TestValidate(new RequestPromoCode.Command(email))
            .ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void A_well_formed_address_passes_the_validator()
    {
        new RequestPromoCode.Validator()
            .TestValidate(new RequestPromoCode.Command(Email))
            .ShouldNotHaveValidationErrorFor(c => c.Email);
    }
}
