namespace Cleansia.Infra.Common.Exceptions;

public sealed class EmailDeliveryException(string message) : Exception(message);