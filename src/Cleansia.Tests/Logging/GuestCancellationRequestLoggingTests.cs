namespace Cleansia.Tests.Logging;

public class GuestCancellationRequestLoggingTests
{
    [Theory]
    [InlineData("/api/Order/Lookup")]
    [InlineData("/api/v1/Order/Lookup")]
    [InlineData("/api/Order/CancelGuest")]
    [InlineData("/api/v1/Order/CancelGuest")]
    [InlineData("/api/Order/GuestCancellationPreview")]
    [InlineData("/api/v1/Order/GuestCancellationPreview")]
    public async Task Every_host_suppresses_both_bodies_on_secret_key_guest_routes(string path)
    {
        const string secrets = "guest-secret@example.test confirmation-secret private-cancel-reason";
        foreach (var middleware in RequestLoggingHarness.AllHostMiddleware)
        {
            var logs = await RequestLoggingHarness.RunAsync(middleware, path,
                responseJson: $$"""{"message":"{{secrets}}"}""",
                requestJson: $$"""{"email":"guest-secret@example.test","confirmationCode":"confirmation-secret","reason":"private-cancel-reason"}""",
                method: "POST");
            var text = string.Join("\n", logs);
            Assert.Contains("[suppressed: sensitive endpoint]", text);
            Assert.DoesNotContain("guest-secret@example.test", text);
            Assert.DoesNotContain("confirmation-secret", text);
            Assert.DoesNotContain("private-cancel-reason", text);
        }
    }
}
