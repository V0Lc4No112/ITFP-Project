namespace Rental.Domain.Results;

public abstract record Option<T>
{
    private Option() { }

    public sealed record Some : Option<T>
    {
        public T Value { get; }

        public Some(T value) =>
            Value = value is null ? throw new ArgumentNullException(nameof(value)) : value;
    }

    public sealed record None : Option<T>;
}

public static class Option
{
    public static Option<T> Some<T>(T value) => new Option<T>.Some(value);

    public static Option<T> None<T>() => new Option<T>.None();

    public static Result<T> ToResult<T>(this Option<T> option, Func<Error> whenMissing) =>
        option switch
        {
            Option<T>.Some some => new Result<T>.Ok(some.Value),
            Option<T>.None => new Result<T>.Fail(whenMissing()),
            _ => throw new InvalidOperationException("Unknown option"),
        };
}
