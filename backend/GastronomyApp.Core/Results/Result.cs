namespace GastronomyApp.Core.Results;

public sealed class Result<TValue, TFailure>
{
    private readonly TValue? _value;
    private readonly TFailure? _failure;

    private Result(bool isSuccess, TValue? value, TFailure? failure)
    {
        IsSuccess = isSuccess;
        _value = value;
        _failure = failure;
    }

    public bool IsSuccess { get; }

    public TValue Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException("The value of a failed result cannot be read.");
            }

            return _value!;
        }
    }

    public TFailure Failure
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("The failure of a successful result cannot be read.");
            }

            return _failure!;
        }
    }

    public static Result<TValue, TFailure> Success(TValue value)
    {
        return new Result<TValue, TFailure>(true, value, default);
    }

    public static Result<TValue, TFailure> Failed(TFailure failure)
    {
        return new Result<TValue, TFailure>(false, default, failure);
    }
}
