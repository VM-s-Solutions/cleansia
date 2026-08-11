using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Services;

public class PayoutReferenceAllocator(IPayoutReferenceCounterRepository counterRepository)
    : IPayoutReferenceAllocator
{
    public async Task<BusinessResult<string>> AllocateAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;

        var ordinal = await counterRepository.AllocateNextAsync(year, cancellationToken);

        if (ordinal is null)
        {
            return BusinessResult.Failure<string>(new Error(
                nameof(EmployeeInvoice.VariableSymbol),
                BusinessErrorMessage.InvoiceReferenceCapacityExhausted));
        }

        return BusinessResult.Success(Format(year, ordinal.Value));
    }

    // The YYYY prefix is what keeps the first digit non-zero, and that is the whole point: a bank form
    // treats the symbol as a number and drops a leading zero, so a stored "0321876543" and a
    // transferred "321876543" stop being the same string on a document whose only job is to make them
    // the same string. The admin lookup is an exact-match filter, which has no near-miss.
    public static string Format(int year, long ordinal) => $"{year:D4}{ordinal:D6}";
}
