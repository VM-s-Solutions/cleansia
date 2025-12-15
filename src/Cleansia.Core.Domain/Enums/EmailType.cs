using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum EmailType
{
    ConfirmationEmail = 1,
    ResetPassword = 2,
    OrderReceipt = 3,
    PeriodClosed = 4,
    PeriodEndReminder = 5,
}