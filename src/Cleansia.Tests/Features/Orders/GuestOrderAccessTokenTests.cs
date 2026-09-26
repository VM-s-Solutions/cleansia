using System.ComponentModel.DataAnnotations.Schema;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The credential that replaced the (display number, e-mail, confirmation code) triple — a triple
/// every assigned cleaner was served in full on the order detail, and which the anonymous guest
/// cancellation accepted. What a credential owes: enough entropy that guessing is not a strategy, no
/// raw value at rest, a life that ends, and a way to end it early.
/// </summary>
public class GuestOrderAccessTokenTests
{
    private static Order GuestOrder(DateTime? cleaningDateTime = null, string? userId = null, string? tenantId = null)
    {
        var order = Order.Create(
            customerName: "Jana",
            customerEmail: "jana@example.test",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Street 1", "Praha", "14000", "country-1"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime ?? new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Pending,
            userId: userId);
        order.Id = "ord-1";
        order.TenantId = tenantId;
        return order;
    }

    [Fact]
    public void The_Raw_Token_Carries_256_Bits_And_Is_Url_Safe()
    {
        var token = GuestOrderAccessToken.Issue("ord-1", DateTimeOffset.UtcNow.AddDays(30));

        // 32 random bytes → 43 unpadded base64url characters. Asserted on the length rather than on
        // the byte count because the length is what a link, a log and a column all see.
        Assert.Equal(43, token.RawToken!.Length);
        Assert.All(token.RawToken, c => Assert.True(
            char.IsAsciiLetterOrDigit(c) || c is '-' or '_',
            $"'{c}' would have to be percent-encoded in the e-mail's link"));
    }

    [Fact]
    public void Two_Issues_Never_Collide()
    {
        var raws = Enumerable.Range(0, 64)
            .Select(_ => GuestOrderAccessToken.Issue("ord-1", DateTimeOffset.UtcNow.AddDays(30)).RawToken!)
            .ToList();

        Assert.Equal(raws.Count, raws.Distinct().Count());
    }

    [Fact]
    public void Only_The_Hash_Is_Persistable()
    {
        var token = GuestOrderAccessToken.Issue("ord-1", DateTimeOffset.UtcNow.AddDays(30));

        Assert.Equal(SecurityTokens.Hash(token.RawToken!), token.TokenHash);
        Assert.DoesNotContain(token.RawToken!, token.TokenHash, StringComparison.Ordinal);

        // The raw carrier is [NotMapped], so nothing but the hash can reach a column.
        Assert.NotEmpty(typeof(GuestOrderAccessToken)
            .GetProperty(nameof(GuestOrderAccessToken.RawToken))!
            .GetCustomAttributes(typeof(NotMappedAttribute), inherit: false));
    }

    [Fact]
    public void The_Expiry_Is_Thirty_Days_Past_The_Cleaning()
    {
        var cleaning = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            new DateTimeOffset(cleaning).AddDays(GuestOrderAccessToken.LifetimeDaysAfterCleaning),
            GuestOrderAccessToken.ExpiryFor(cleaning));
    }

    [Fact]
    public void A_Token_Is_Live_Until_It_Expires_Or_Is_Revoked()
    {
        var now = DateTimeOffset.UtcNow;
        var live = GuestOrderAccessToken.Issue("ord-1", now.AddDays(30));
        var expired = GuestOrderAccessToken.Issue("ord-1", now.AddSeconds(-1));
        var revoked = GuestOrderAccessToken.Issue("ord-1", now.AddDays(30)).Revoke(now);

        Assert.True(live.IsLive(now));
        Assert.False(expired.IsLive(now));
        Assert.False(revoked.IsLive(now));
    }

    [Fact]
    public void Revocation_Keeps_The_Instant_It_First_Happened()
    {
        var first = DateTimeOffset.UtcNow.AddHours(-2);
        var token = GuestOrderAccessToken.Issue("ord-1", DateTimeOffset.UtcNow.AddDays(30)).Revoke(first);

        token.Revoke(DateTimeOffset.UtcNow);

        Assert.Equal(first, token.RevokedOn);
    }

    [Fact]
    public void The_Issuer_Mints_For_A_Guest_Booking_And_Stamps_Its_Company()
    {
        var repository = new Mock<IGuestOrderAccessTokenRepository>();
        GuestOrderAccessToken? added = null;
        repository.Setup(r => r.Add(It.IsAny<GuestOrderAccessToken>()))
            .Callback<GuestOrderAccessToken>(t => added = t);

        var raw = new GuestOrderAccessTokenIssuer(repository.Object)
            .IssueForGuest(GuestOrder(tenantId: "cleansia-cz"));

        Assert.NotNull(raw);
        Assert.NotNull(added);
        Assert.Equal("ord-1", added!.OrderId);
        Assert.Equal("cleansia-cz", added.TenantId);
        Assert.Equal(SecurityTokens.Hash(raw!), added.TokenHash);
    }

    [Fact]
    public void The_Issuer_Mints_Nothing_For_An_Account_Booking()
    {
        var repository = new Mock<IGuestOrderAccessTokenRepository>(MockBehavior.Strict);

        var raw = new GuestOrderAccessTokenIssuer(repository.Object).IssueForGuest(GuestOrder(userId: "user-1"));

        Assert.Null(raw);
        repository.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Re-issuing used to supersede, and that is the defect the second issuance channel exposed: every
    /// later message with a track link killed the link in the message before it, so "your cleaner is on
    /// the way" retired the confirmation e-mail's link and the checkout response's with it. Each issue
    /// is now an independent 256-bit credential and they all stay live.
    /// </summary>
    [Fact]
    public void Re_Issuing_Supersedes_Nothing_And_Both_Stay_Live()
    {
        var repository = new Mock<IGuestOrderAccessTokenRepository>();
        var minted = new List<GuestOrderAccessToken>();
        repository.Setup(r => r.Add(It.IsAny<GuestOrderAccessToken>()))
            .Callback<GuestOrderAccessToken>(minted.Add);
        var issuer = new GuestOrderAccessTokenIssuer(repository.Object);
        // A cleaning still to come: the expiry hangs off it, so a fixture booked in the past mints
        // tokens that were never live and the liveness assertion below would prove nothing.
        var order = GuestOrder(cleaningDateTime: DateTime.UtcNow.AddDays(3));

        var first = issuer.IssueForGuest(order);
        var second = issuer.IssueForGuest(order);

        Assert.Equal(2, minted.Count);
        Assert.NotEqual(first, second);
        Assert.All(minted, t => Assert.True(t.IsLive(DateTimeOffset.UtcNow)));
        Assert.Equal([SecurityTokens.Hash(first!), SecurityTokens.Hash(second!)], minted.Select(t => t.TokenHash));
        // Minting reads nothing: the row it would have had to revoke is the whole reason it used to.
        repository.Verify(
            r => r.GetLiveForOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Revoking_A_Booking_Retires_Every_Live_Token_It_Has()
    {
        var now = DateTimeOffset.UtcNow;
        var live = new[]
        {
            GuestOrderAccessToken.Issue("ord-1", now.AddDays(30)),
            GuestOrderAccessToken.Issue("ord-1", now.AddDays(30)),
        };
        var repository = new Mock<IGuestOrderAccessTokenRepository>();
        repository.Setup(r => r.GetLiveForOrderIgnoringTenantAsync("ord-1", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(live);

        await new GuestOrderAccessTokenIssuer(repository.Object).RevokeAsync(GuestOrder(), CancellationToken.None);

        Assert.All(live, t => Assert.False(t.IsLive(DateTimeOffset.UtcNow)));
    }
}
