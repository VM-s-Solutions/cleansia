namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface ISendGridConfig
{
    public string ApiKey { get; set; }
    public string AddressFrom { get; set; }
    public string OrderReceiptTemplateId { get; set; }
    public string EmailConfirmationTemplateId { get; set; }
    public string ResetPasswordTemplateId { get; set; }
    public string PeriodClosedTemplateId { get; set; }
    public string PeriodEndReminderTemplateId { get; set; }
    public string OrderStatusUpdateTemplateId { get; set; }
    public string ClientDomainUrl { get; set; }
    public string ResetPasswordUrl { get; set; }
    public string EmailConfirmationUrl { get; set; }
    public string OrderStatusUrl { get; set; }
}