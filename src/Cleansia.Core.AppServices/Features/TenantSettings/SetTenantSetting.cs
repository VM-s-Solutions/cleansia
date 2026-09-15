using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// Sets one catalogued key for the admin's own operating company — the row is created or updated in
/// place, and what is stored is the catalogue's canonical form of the value, never the text as typed.
/// The company is the ambient tenant: the query filter finds its row and the commit stamps a new one.
/// </summary>
[AuditAction("tenant_setting.set", ResourceType = "TenantSetting")]
public class SetTenantSetting
{
    public record Command(string Key, string Value) : ICommand<Response>;

    public record Response(string Key, string Value);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Key)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must(key => TenantSettingCatalog.Find(key) is not null)
                .WithMessage(BusinessErrorMessage.TenantSettingUnknownKey);

            RuleFor(x => x.Value)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must((command, value) => TenantSettingCatalog.Find(command.Key)?.IsValid(value) ?? true)
                .WithMessage(BusinessErrorMessage.TenantSettingInvalidValue);
        }
    }

    public class Handler(
        ITenantConfigurationRepository tenantConfigurationRepository,
        IAuditContext auditContext) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // The validator refused any key outside the catalogue and any value its definition rejects.
            var definition = TenantSettingCatalog.Find(command.Key)!;
            var value = definition.Canonicalize(command.Value)!;

            var existing = await tenantConfigurationRepository.GetByKeyAsync(definition.Key, cancellationToken);
            var before = new TenantSettingSnapshot(definition.Key, existing?.Value);

            if (existing is null)
            {
                tenantConfigurationRepository.Add(TenantConfiguration.Create(definition.Key, value, category: definition.Category));
            }
            else
            {
                existing.UpdateValue(value);
            }

            auditContext.RecordChange("TenantSetting", definition.Key, before, new TenantSettingSnapshot(definition.Key, value));

            return BusinessResult.Success(new Response(definition.Key, value));
        }
    }
}
