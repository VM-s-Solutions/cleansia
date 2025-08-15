namespace Cleansia.Core.Domain.Extensions;

public static class OrderExtensions
{
    public static string GenerateConfirmationCode()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
    }
}