using Rental.Domain.States;
using System.Collections.Immutable;

namespace Rental.Domain.Results;

public abstract record Result<T>
{
    private Result() { }

    public sealed record Ok : Result<T>
    {
        public T Value { get; }

        public Ok(T value) =>
            Value = value is null ? throw new ArgumentNullException(nameof(value)) : value;
    }

    public sealed record Fail : Result<T>
    {
        public Error Error { get; }

        public Fail(Error error) =>
            Error = error is null ? throw new ArgumentNullException(nameof(error)) : error;
    }
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new Result<T>.Ok(value);

    public static Result<T> Fail<T>(Error error) => new Result<T>.Fail(error);

    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> map) =>
        result switch
        {
            Result<T>.Ok ok => new Result<U>.Ok(map(ok.Value)),
            Result<T>.Fail fail => new Result<U>.Fail(fail.Error),
            _ => throw new InvalidOperationException("Unknown result"),
        };

    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> next) =>
        result switch
        {
            Result<T>.Ok ok => next(ok.Value),
            Result<T>.Fail fail => new Result<U>.Fail(fail.Error),
            _ => throw new InvalidOperationException("Unknown result"),
        };

    public static Result<T> ToResult<T>(
        this Validation<T> validation, Func<ImmutableArray<string>, Error> whenInvalid) =>
        validation switch
        {
            Validation<T>.Valid valid => new Result<T>.Ok(valid.Value),
            Validation<T>.Invalid invalid => new Result<T>.Fail(whenInvalid(invalid.Reasons)),
            _ => throw new InvalidOperationException("Unknown validation"),
        };
}
