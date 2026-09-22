using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// The deterministic-key property for the async-email effect. The key is a pure function of (email
/// type, user id, generated-code hash): the same logical email emits the same key, so a duplicate
/// enqueue (a SendGrid-retry re-send of the same logical email) and a queue redelivery collapse onto
/// one key and the consumer recognizes the email as already-done. A re-issued token (a genuine resend)
/// changes the code-hash segment, so it is a distinct key and a new email is sent.
/// </summary>
public class EmailMessageKeyTests
{
    private const string UserId = "USER-1";
    private const string CodeHash = "abc123hash";

    [Fact]
    public void Email_Key_Follows_Frozen_Formula()
    {
        Assert.Equal(
            "email:confirmation:USER-1:abc123hash",
            MessageKeys.Email(EmailType.ConfirmationEmail, UserId, CodeHash));
    }

    [Fact]
    public void Email_Key_Discriminates_By_Type()
    {
        Assert.NotEqual(
            MessageKeys.Email(EmailType.ConfirmationEmail, UserId, CodeHash),
            MessageKeys.Email(EmailType.ResetPassword, UserId, CodeHash));
    }

    [Fact]
    public void Admin_Notification_Key_Hashes_The_Address_And_Discriminates_By_Event_Subject_And_Address()
    {
        var key = MessageKeys.AdminNotificationEmail("admin.dispute.filed", "dispute-1", "Ops@Example.com");

        Assert.StartsWith("admin-email:admin.dispute.filed:dispute-1:", key, StringComparison.Ordinal);
        Assert.DoesNotContain("@", key);
        Assert.DoesNotContain("example", key, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(key, MessageKeys.AdminNotificationEmail("admin.dispute.filed", "dispute-1", " ops@example.com "));
        Assert.NotEqual(key, MessageKeys.AdminNotificationEmail("admin.dispute.filed", "dispute-2", "ops@example.com"));
        Assert.NotEqual(key, MessageKeys.AdminNotificationEmail("admin.dispute.chargeback", "dispute-1", "ops@example.com"));
        Assert.NotEqual(key, MessageKeys.AdminNotificationEmail("admin.dispute.filed", "dispute-1", "other@example.com"));
    }

    [Fact]
    public void Email_Key_Is_Deterministic_For_Same_Inputs()
    {
        Assert.Equal(
            MessageKeys.Email(EmailType.ResetPassword, UserId, CodeHash),
            MessageKeys.Email(EmailType.ResetPassword, UserId, CodeHash));
    }

    [Fact]
    public void Email_Key_Changes_When_The_Code_Is_Reissued()
    {
        // A genuine resend rotates the token (new hash) → a new key → a new email is sent, while a
        // redelivery of the SAME logical email keeps the same hash and dedups.
        Assert.NotEqual(
            MessageKeys.Email(EmailType.ConfirmationEmail, UserId, "hash-v1"),
            MessageKeys.Email(EmailType.ConfirmationEmail, UserId, "hash-v2"));
    }
}
