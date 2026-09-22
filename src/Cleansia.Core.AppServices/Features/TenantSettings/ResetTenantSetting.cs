using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// Puts one catalogued key back to its default for the admin's own operating company by removing the
/// row. A hard delete rather than a deactivation: the row is a value override with no history of its
/// own, and the unique (TenantId, Key) index would refuse the next Set if a deactivated row stayed.
/// Idempotent — a key with no row is already at its default.
/// </summary>
[AuditAction("tenant_setting.reset", ResourceType = "TenantSetting")]
public class ResetTenantSetting
{
    public record Command(string Key) : ICommand<Response>;

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
        }
    }

    public class Handler(
        ITenantConfigurationRepository tenantConfigurationRepository,
        IAuditContext auditContext) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // The validator refused any key outside the catalogue.
            var definition = TenantSettingCatalog.Find(command.Key)!;

            var existing = await tenantConfigurationRepository.GetByKeyAsync(definition.Key, cancellationToken);
            if (existing is not null)
            {
                tenantConfigurationRepository.Remove(existing);
            }

            auditContext.RecordChange(
                "TenantSetting",
                definition.Key,
                new TenantSettingSnapshot(definition.Key, existing?.Value),
                new TenantSettingSnapshot(definition.Key, null));

            return BusinessResult.Success(new Response(definition.Key, definition.DefaultValue));
        }
    }
}
