using Cleansia.Core.Fiscal.Abstractions;

namespace Cleansia.Core.AppServices.Features.FiscalFailures.DTOs;

public record FiscalFailureDto(
    string ReceiptId,
    string ReceiptNumber,
    string OrderId,
    string? OrderNumber,
    DateTime IssuedAt,
    string? FiscalProviderKey,
    FiscalErrorKind? ErrorKind,
    string? ErrorMessage,
    int RetryCount,
    DateTime? LastRetryAt,
    DateTime? NextRetryAt,
    bool Acknowledged);
