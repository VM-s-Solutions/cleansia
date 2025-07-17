namespace Cleansia.Infra.Common.Validations;

public class BusinessResult<TValue> : BusinessResult
{
    private readonly TValue? value;

    protected internal BusinessResult(bool isSuccess = false, Error? error = default, TValue? value = default)
        : base(isSuccess, error) =>
        this.value = value;

    public TValue Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("The value of a failure result can not be accessed.");

    public static implicit operator BusinessResult<TValue>(TValue? value) => Create(value);
}