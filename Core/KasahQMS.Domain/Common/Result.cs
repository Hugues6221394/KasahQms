namespace KasahQMS.Domain.Common;

/// <summary>
/// Represents the result of an operation that can fail.
/// Use this instead of throwing exceptions for expected failures.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string ErrorMessage { get; }
    
    protected Result(bool isSuccess, string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }
    
    public static Result Success() => new(true, string.Empty);
    public static Result<T> Success<T>(T value) => new(value, true, string.Empty);
    public static Result Failure(string error) => new(false, error);
    public static Result<T> Failure<T>(string error) => new(default!, false, error);
    
    public static Result FirstFailureOrSuccess(params Result[] results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
                return result;
        }
        return Success();
    }
}

/// <summary>
/// Result with a value of type T.
/// </summary>
public class Result<T> : Result
{
    private readonly T _value;
    
    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access value of a failed result.");
            
            return _value;
        }
    }
    
    protected internal Result(T value, bool isSuccess, string errorMessage) : base(isSuccess, errorMessage)
    {
        _value = value;
    }
    
    public static implicit operator Result<T>(T value) => Success(value);
}
