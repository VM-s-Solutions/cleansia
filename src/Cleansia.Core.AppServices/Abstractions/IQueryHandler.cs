using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Abstractions;

public interface IQueryHandler<in TQuery, TResponse>
    : IRequestHandler<TQuery, BusinessResult<TResponse>>
    where TQuery : IQuery<TResponse>;