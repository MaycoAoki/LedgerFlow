namespace LedgerFlow.Shared.Result;

public class Result
{
    public bool IsSuccess { get; }
    public IReadOnlyList<Error> Errors { get; }

    protected Result(bool isSuccess, IEnumerable<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        Errors = errors?.ToList() ?? new List<Error>();
    }

    public static Result Ok() => new(true);
    public static Result Fail(params Error[] errors) => new(false, errors);
    public static Result Fail(IEnumerable<Error> errors) => new(false, errors);
}
