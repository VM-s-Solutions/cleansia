namespace Cleansia.Infra.Common.Exceptions;

public sealed class EntityNotFoundException(string message) : Exception(message);