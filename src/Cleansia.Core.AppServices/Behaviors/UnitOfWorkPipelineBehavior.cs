using Cleansia.Core.Domain.SeedWork;
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
        await unitOfWork.CommitAsync(cancellationToken);

        return response;
    }

    private static bool IsNotCommand(TRequest request)
    {
        return !request.GetType().Name.EndsWith(Command);
    }
}
