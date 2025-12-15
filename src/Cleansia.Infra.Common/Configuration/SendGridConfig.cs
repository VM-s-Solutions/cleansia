using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class SendGridConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "SendGrid"), ISendGridConfig
{
    public string ApiKey { get; set; } = null!;
    public string AddressFrom { get; set; } = null!;
    public string OrderReceiptTemplateId { get; set; } = null!;
    public string EmailConfirmationTemplateId { get; set; } = null!;
    public string ResetPasswordTemplateId { get; set; } = null!;
    public string PeriodClosedTemplateId { get; set; } = null!;
    public string PeriodEndReminderTemplateId { get; set; } = null!;
    public string ClientDomainUrl { get; set; } = null!;
    public string ResetPasswordUrl { get; set; } = null!;
    public string EmailConfirmationUrl { get; set; } = null!;
    public string OrderStatusUrl { get; set; } = null!;
}