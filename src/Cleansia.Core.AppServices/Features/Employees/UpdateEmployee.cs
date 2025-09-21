using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdateEmployee
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            
        }
    }

    public record Command(int Id, string Name, string Email) : ICommand<string>;

    public class Handler : ICommandHandler<Command, string>
    {
        public Task<BusinessResult<string>> Handle(Command request, CancellationToken cancellationToken)
        {

        }
    }
}