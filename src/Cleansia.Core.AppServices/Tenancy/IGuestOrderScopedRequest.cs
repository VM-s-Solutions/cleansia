namespace Cleansia.Core.AppServices.Tenancy;

public interface IGuestOrderScopedRequest : IOperatorScopedRequest
{
    string DisplayOrderNumber { get; }
    string Email { get; }
    string ConfirmationCode { get; }
}
