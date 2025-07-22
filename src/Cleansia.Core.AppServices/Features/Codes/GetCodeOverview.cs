using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using MediatR;
using DomainAssemblyReference = Cleansia.Core.Domain.AssemblyReference;

namespace Cleansia.Core.AppServices.Features.Codes;

public class GetCodeOverview
{
    public record Request : IRequest<IEnumerable<Code>>;

    public record Handler : IRequestHandler<Request, IEnumerable<Code>>
    {
        public Task<IEnumerable<Code>> Handle(Request request, CancellationToken cancellationToken)
            => Task.FromResult(DomainAssemblyReference.Assembly.MapToCodeFromAssembly());
    }
}