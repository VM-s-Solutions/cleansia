using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The confirmation of a contract on a durable medium, as a PDF built from what the platform stored when
/// the contract was concluded.
/// </summary>
public interface IContractConfirmationService
{
    /// <summary>
    /// The customer's booking confirmation: the seller as its company record names it, the booking, the
    /// price, when the contract was concluded, the terms in force when the booking was made and the
    /// request to start within the withdrawal period. <paramref name="order"/> is loaded with its address
    /// and currency.
    /// </summary>
    Task<(byte[] Bytes, string FileName)> ForBookingAsync(
        Order order, DateTimeOffset contractConcludedOn, string languageCode, CancellationToken cancellationToken);

    /// <summary>
    /// The cleaner's copy of the contract for work of one seat: the operating company and the cleaner, the
    /// job, the seat's reward, when it was accepted, the version and fingerprint of the text accepted, and
    /// that text. <paramref name="cleaner"/> is loaded with its user.
    /// </summary>
    Task<(byte[] Bytes, string FileName)> ForWorkContractAsync(
        WorkContractAcceptance acceptance, Employee cleaner, string languageCode, CancellationToken cancellationToken);
}
