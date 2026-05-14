namespace LedgerFlow.Shared.Result;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<Error> Errors { get; }

    protected Result(bool isSuccess, T? value, IEnumerable<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors?.ToList() ?? new List<Error>();
    }

    public static Result<T> Ok(T value) => new(true, value);
    public static Result<T> Fail(params Error[] errors) => new(false, default, errors);
    public static Result<T> Fail(IEnumerable<Error> errors) => new(false, default, errors);
}
