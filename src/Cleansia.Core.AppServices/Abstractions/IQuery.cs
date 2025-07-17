using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Abstractions;

public interface IQuery<TResponse> : IRequest<BusinessResult<TResponse>>;