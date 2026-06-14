using SendGrid;

namespace Cleansia.Core.Clients.Abstractions.SendGrid;

public interface ISendGridClientFactory
{
    ISendGridClient CreateClient();
}