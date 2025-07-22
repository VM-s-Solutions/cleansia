using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class StripeConfig(IConfiguration configuration) : AutoBindConfig(configuration, "Stripe"), IStripeConfig
{
    public string SecretKey { get; set; } = null!;
    public string PublishableKey { get; set; } = null!;
    public string WebhookSecret { get; set; } = null!;
    public string WebhookUrl { get; set; } = null!;
    public string SuccessUrlBase { get; set; } = null!;
    public string CancelUrlBase { get; set; } = null!;
}