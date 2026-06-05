using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Behaviors;

public class UnitOfWorkPipelineBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const string Command = nameof(Command);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (IsNotCommand(request))
        {
            return await next(cancellationToken);
        }

        var response = await next(cancellationToken);

        // ADR-0002 D4 (F11) defense-in-depth: commit ONLY when the inner pipeline produced a
        // successful BusinessResult. Combined with the Validation-outer registration order, a future
        // re-swap of the registration cannot resurrect F11 (a committed validation failure). TResponse
        // is constrained to BusinessResult on the validation behavior, so PagedData<T> queries never
        // reach this branch (the IsNotCommand guard above already skips them too).
        if (response is BusinessResult { IsSuccess: true })
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }

        return response;
    }

    private static bool IsNotCommand(TRequest request)
    {
        return !request.GetType().Name.EndsWith(Command);
    }
}
