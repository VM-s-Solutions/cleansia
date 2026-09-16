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
    OrderStatusUpdate = 6,

    /// <summary>First-order discount code sent to an address with no account yet.</summary>
    PromoCode = 7,

    /// <summary>A customer of a company that is winding down: the date, the refunds, Plus, credit, the account.</summary>
    CompanyWindDownCustomer = 8,

    /// <summary>A cleaner of a company that is winding down: the last day, the last pay period, sign-in ending.</summary>
    CompanyWindDownCleaner = 9,
}