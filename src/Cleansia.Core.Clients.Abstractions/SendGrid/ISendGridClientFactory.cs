using SendGrid;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.Clients.Abstractions.SendGrid;

public interface ISendGridClientFactory
{
    ISendGridClient CreateClient();

    Task<BusinessResult> SendTemplateEmailAsync(ISendGridClient client, string from, string to, string templateId, object data, CancellationToken cancellationToken = default);
}