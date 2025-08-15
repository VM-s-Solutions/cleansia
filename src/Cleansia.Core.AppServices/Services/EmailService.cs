using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using SendGrid;

namespace Cleansia.Core.AppServices.Services;

public sealed class EmailService : IEmailService
{
    private readonly ISendGridConfig sendGridConfig;
    private readonly ILogger<EmailService> logger;
    private readonly IAsyncPolicy<Response> policy;

    public EmailService(ISendGridConfig cfg, ILogger<EmailService> log)
    {
        sendGridConfig = cfg;
        logger = log;

        policy = Policy
            .HandleResult<Response>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                i => TimeSpan.FromMilliseconds(i * 300),
                (outcome, delay, attempt, _) =>
                    logger.LogWarning(
                        "SendGrid attempt {Attempt} failed ({Status}). Retrying in {Delay} ms.",
                        attempt,
                        outcome.Result?.StatusCode,
                        delay.TotalMilliseconds));
    }

    public Task<string> SendResetPasswordEmailAsync(string email, string fullUserName, string code, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> SendOrderReceiptEmailAsync(string email, Order order, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> SendEmailConfirmationAsync(string email, string userName, string verificationCode, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}