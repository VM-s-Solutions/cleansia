using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Behaviors;

public class UnitOfWorkPipelineBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const string Command = nameof(Command);
    private static readonly bool ResponseCarriesFailure = typeof(BusinessResult).IsAssignableFrom(typeof(TResponse));

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (IsNotCommand(request))
        {
            return await next(cancellationToken);
        }

        try
        {
            var response = await next(cancellationToken);

            // ADR-0002 D4 (F11) defense-in-depth: commit ONLY when the inner pipeline produced a
            // successful BusinessResult. Combined with the Validation-outer registration order, a future
            // re-swap of the registration cannot resurrect F11 (a committed validation failure). A
            // PagedData<T> query never reaches this branch: the IsNotCommand guard above skips it.
            if (response is BusinessResult { IsSuccess: true })
                await unitOfWork.CommitAsync(cancellationToken);
            else
                unitOfWork.Rollback();

            return response;
        }
        catch (DbUpdateConcurrencyException ex) when (ResponseCarriesFailure
            && ex.Entries.Any(e => e.Entity is EmployeeInvoice))
        {
            // Another admin action on the invoice committed after this one read it.
            unitOfWork.Rollback();
            return ValidationPipelineBehavior<IRequest<TResponse>, TResponse>.CreateValidationResult<TResponse>(
                [new Error(nameof(EmployeeInvoice), BusinessErrorMessage.InvoiceChangedConcurrently)]);
        }
        catch
        {
            // A failed command releases its credit locks now, not when its scope is disposed.
            unitOfWork.Rollback();
            throw;
        }
    }

    private static bool IsNotCommand(TRequest request)
    {
        return !request.GetType().Name.EndsWith(Command);
    }
}
