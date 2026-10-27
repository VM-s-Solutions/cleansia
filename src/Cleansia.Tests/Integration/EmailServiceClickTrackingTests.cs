using System.Net;
using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Integration;

/// <summary>
/// Every e-mail goes to SendGrid with click tracking off, whatever the account's default. A tracked button is a
/// SendGrid redirect whose click log keeps the full target — the guest booking's token, the reset code — and no
/// redirect-wrapped link can open the booking it names; with tracking off each button is the real link. All the
/// platform's e-mails leave through one send, so asking two of them asks all of them.
/// </summary>
public sealed class EmailServiceClickTrackingTests
{
    private const string Recipient = "jana.novak@cleansia.test";

    [Fact]
    public async Task A_Password_Reset_Email_Is_Sent_With_Click_Tracking_Off()
    {
        var capture = new CapturingHandler();

        await BuildService(capture).SendResetPasswordEmailAsync(Recipient, "Jana Nováková", "123456", "en", CancellationToken.None);

        AssertClickTrackingOff(capture.Body);
    }

    [Fact]
    public async Task An_Email_Confirmation_Is_Sent_With_Click_Tracking_Off()
    {
        var capture = new CapturingHandler();

        await BuildService(capture).SendEmailConfirmationAsync(Recipient, "Jana Nováková", "123456", "en", CancellationToken.None);

        AssertClickTrackingOff(capture.Body);
    }

    private static void AssertClickTrackingOff(string? body)
    {
        Assert.NotNull(body);
        var clickTracking = JsonDocument.Parse(body!).RootElement
            .GetProperty("tracking_settings")
            .GetProperty("click_tracking");
        Assert.False(clickTracking.GetProperty("enable").GetBoolean());
        Assert.False(clickTracking.GetProperty("enable_text").GetBoolean());
    }

    private static EmailService BuildService(HttpMessageHandler handler)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");
        config.SetupGet(c => c.ResetPasswordUrl).Returns("/forgot-password");

        var translations = new Mock<IEmailTemplateTranslationRepository>();
        translations
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Dictionary<string, string> { ["Subject"] = "Cleansia" });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        return new EmailService(
            config.Object,
            NullLogger<EmailService>.Instance,
            httpClientFactory.Object,
            translations.Object,
            new EmailTemplateRenderer(),
            Mock.Of<ICountryConfigurationRepository>(),
            Mock.Of<ICompanyInfoRepository>());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
