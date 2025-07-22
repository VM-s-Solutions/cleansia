namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IStripeConfig
{
    string SecretKey { get; set; }
    string PublishableKey { get; set; }
    string WebhookSecret { get; set; }
    string WebhookUrl { get; set; }
    string SuccessUrlBase { get; set; }
    string CancelUrlBase { get; set; }
}