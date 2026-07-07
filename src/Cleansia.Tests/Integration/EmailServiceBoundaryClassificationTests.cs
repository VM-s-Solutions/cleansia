using System.Net;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The SendGrid boundary in <see cref="EmailService"/>: a failed send is classified and a
/// <c>Permanent</c> (bad template / invalid recipient 4xx) or <c>AuthConfig</c> (401/403) failure is
/// surfaced as an <see cref="EmailDeliveryException"/> WITHOUT the SDK looping the call itself, and
/// records the owner-alert counter. The transport-level retry budget (Transient retried, AuthConfig
/// not) is proven at the named-client layer; this test pins the adapter-side behavior: exactly one
/// SDK call, and a recorded metric.
/// </summary>
[Collection("IntegrationFailureMeter")]
public class EmailServiceBoundaryClassificationTests
{
    private const string Recipient = "user@example.com";

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, IntegrationFailureClass.AuthConfig)]
    [InlineData(HttpStatusCode.Forbidden, IntegrationFailureClass.AuthConfig)]
    [InlineData(HttpStatusCode.BadRequest, IntegrationFailureClass.Permanent)]
    public async Task NonRetryable_Failure_Throws_After_A_Single_Send_And_Records_Metric(
        HttpStatusCode status, IntegrationFailureClass expectedClass)
    {
        var spy = new AttemptCountingHandler(status);
        var measurements = FailureMetricsCapture.Start(out var listener);
        using (listener)
        {
            var service = BuildService(spy);

            await Assert.ThrowsAsync<EmailDeliveryException>(
                () => service.SendEmailConfirmationAsync(Recipient, "John", "code-1", "en", CancellationToken.None));
        }

        Assert.Equal(1, spy.Attempts);
        Assert.Contains(measurements, m => m.Provider == "SendGrid" && m.Class == expectedClass.ToString());
        Assert.DoesNotContain(measurements,
            m => m.Provider == "SendGrid" && m.Class != expectedClass.ToString());
    }

    private static EmailService BuildService(HttpMessageHandler primaryHandler)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.EmailConfirmationTemplateId).Returns("d-template-1");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translations = new Mock<IEmailTemplateTranslationRepository>();
        translations
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Subject"] = "Confirm Your Email" });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(primaryHandler, disposeHandler: false));

        return new EmailService(
            config.Object,
            NullLogger<EmailService>.Instance,
            httpClientFactory.Object,
            translations.Object);
    }

    private sealed class AttemptCountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        private int _attempts;

        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"errors":[{"message":"boom"}]}"""),
            });
        }
    }
}
