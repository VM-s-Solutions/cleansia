using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Mappers;

public static class PayoutDetailsMappers
{
    private const int VisibleTailLength = 4;

    public static MyPayoutDetails MapToMyDto(this EmployeePayoutDetails details) => new(
        Scheme: details.Scheme,
        Status: details.Status,
        BankCountryId: details.BankCountryId,
        AccountPrefix: details.AccountPrefix,
        AccountNumber: details.AccountNumber,
        BankCode: details.BankCode,
        Iban: details.Iban,
        Swift: details.Swift,
        BankName: details.BankName,
        HolderName: details.HolderName,
        ConfirmedAt: details.ConfirmedAt);

    public static MaskedPayoutDetails MapToMaskedDto(this EmployeePayoutDetails details) => new(
        EmployeeId: details.EmployeeId,
        Scheme: details.Scheme,
        Status: details.Status,
        BankCountryId: details.BankCountryId,
        MaskedAccount: Mask(details.AccountNumber ?? details.Iban),
        BankName: details.BankName,
        ConfirmedAt: details.ConfirmedAt,
        LastRevealedAt: details.LastRevealedAt,
        RevealCount: details.RevealCount);

    public static RevealedPayoutDetails MapToRevealedDto(this EmployeePayoutDetails details) => new(
        EmployeeId: details.EmployeeId,
        Scheme: details.Scheme,
        AccountPrefix: details.AccountPrefix,
        AccountNumber: details.AccountNumber,
        BankCode: details.BankCode,
        Iban: details.Iban,
        Swift: details.Swift,
        BankName: details.BankName,
        HolderName: details.HolderName);

    private static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= VisibleTailLength
            ? new string('*', trimmed.Length)
            : $"****{trimmed[^VisibleTailLength..]}";
    }
}
