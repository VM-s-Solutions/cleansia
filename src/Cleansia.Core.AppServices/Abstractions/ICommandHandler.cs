using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Abstractions;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, BusinessResult>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse>
    : IRequestHandler<TCommand, BusinessResult<TResponse>>
    where TCommand : ICommand<TResponse>;