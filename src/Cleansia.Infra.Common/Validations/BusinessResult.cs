namespace Cleansia.Infra.Common.Validations;

public class BusinessResult
{
    protected internal BusinessResult(bool isSuccess, Error? error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException();
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException();
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static BusinessResult Success() => new(true, Error.None);

    public static BusinessResult<TValue> Success<TValue>(TValue value) => new(true, null, value);

    public static BusinessResult Failure(Error error) => new(false, error);

    public static BusinessResult<TValue> Failure<TValue>(Error error) => new(false, error, default);

    public static BusinessResult<TValue> Create<TValue>(TValue? value) => value is not null ? Success(value) : Failure<TValue>(Error.NullValue);
}
