namespace Cleansia.Core.Queue.Abstractions.Messages;

public record SendGuestOrderCancellationEmailMessage(
    string OrderId, string LanguageCode, decimal? SuccessfulRefundAmount, string? TenantId)
{
    public string MessageType => "guest-order-cancelled";
}

