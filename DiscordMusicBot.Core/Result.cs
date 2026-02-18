namespace DiscordMusicBot.Core;

public class Result
{
    protected Result(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? ErrorMessage { get; }

    public static Result Success() => new(isSuccess: true, errorMessage: null);

    public static Result Failure(string errorMessage) => new(isSuccess: false, errorMessage);
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, string? errorMessage)
        : base(isSuccess, errorMessage)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(isSuccess: true, value, errorMessage: null);

    public new static Result<T> Failure(string errorMessage) => new(isSuccess: false, default, errorMessage);
}