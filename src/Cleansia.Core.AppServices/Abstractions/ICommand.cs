using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Abstractions;

public interface ICommand : IRequest<BusinessResult>;

public interface ICommand<TResponse> : IRequest<BusinessResult<TResponse>>;