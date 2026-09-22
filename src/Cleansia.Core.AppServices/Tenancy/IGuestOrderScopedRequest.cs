namespace Cleansia.Core.AppServices.Tenancy;

public interface IGuestOrderScopedRequest : IOperatorScopedRequest
{
    string AccessToken { get; }
}
