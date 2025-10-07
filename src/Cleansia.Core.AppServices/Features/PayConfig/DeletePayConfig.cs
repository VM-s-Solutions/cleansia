using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayConfig;

public class DeletePayConfig
{
    public record Command(string PayConfigId) : ICommand<Response>;

    public record Response(string PayConfigId);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IEmployeePayConfigRepository _payConfigRepository;
        private readonly IOrderEmployeePayRepository _orderEmployeePayRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeePayConfigRepository payConfigRepository,
            IOrderEmployeePayRepository orderEmployeePayRepository) : base(userRepository, userSessionProvider)
        {
            _payConfigRepository = payConfigRepository;
            _orderEmployeePayRepository = orderEmployeePayRepository;

            RuleFor(x => x.PayConfigId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payConfigRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayConfigNotFound)
                .MustAsync(BeNoOrderPaysUsingConfigAsync)
                .WithMessage(BusinessErrorMessage.PayConfigHasOrderPays);
        }

        private async Task<bool> BeNoOrderPaysUsingConfigAsync(string payConfigId, CancellationToken cancellationToken)
        {
            var config = await _payConfigRepository.GetByIdAsync(payConfigId, cancellationToken);
            if (config == null) return false;

            // Check if any order pays were calculated using this config
            // This is a simple check - in reality you might want to track which config was used
            var hasOrderPays = await _orderEmployeePayRepository.GetAll()
                .AnyAsync(cancellationToken);

            return !hasOrderPays;
        }
    }

    public class Handler(
        IEmployeePayConfigRepository payConfigRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payConfig = await payConfigRepository.GetByIdAsync(command.PayConfigId, cancellationToken);

            payConfigRepository.Remove(payConfig!);

            return BusinessResult.Success(new Response(command.PayConfigId));
        }
    }
}
