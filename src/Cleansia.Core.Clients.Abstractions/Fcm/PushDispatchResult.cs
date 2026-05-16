namespace Cleansia.Core.Clients.Abstractions.Fcm;

/// <summary>
/// Outcome of a multi-token FCM send. The dispatch Function uses
/// <see cref="InvalidTokens"/> to prune dead <c>Device</c> rows after
/// FCM returns 410 / NotRegistered for a token.
/// </summary>
/// <param name="SuccessCount">Tokens that received the push.</param>
/// <param name="FailureCount">Tokens that failed for any reason.</param>
/// <param name="InvalidTokens">
/// Subset of input tokens that FCM rejected as permanently invalid
/// (NotRegistered, InvalidArgument, etc.). Caller deletes the matching
/// <c>Device</c> rows.
/// </param>
public record PushDispatchResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> InvalidTokens);
