using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IEmailService
{
    Task<string> SendResetPasswordEmailAsync(string email, string fullUserName, string code, CancellationToken ct = default);

    Task<string> SendOrderReceiptEmailAsync(string email, Order order, CancellationToken ct = default);

    Task<string> SendEmailConfirmationAsync(string email, string userName, string verificationCode, CancellationToken ct = default);
}