namespace Cleansia.Core.Queue.Abstractions.Messages;

public record SendGuestOrderCancellationEmailMessage(
    string OrderId, string LanguageCode, decimal? SuccessfulRefundAmount, string? TenantId,
    decimal? ApologyCreditAmount = null)
{
    public string MessageType => "guest-order-cancelled";
}

