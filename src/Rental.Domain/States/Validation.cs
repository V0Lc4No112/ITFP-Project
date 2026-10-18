using System.Collections.Immutable;

namespace Rental.Domain.States;

public abstract record Validation<T>
{
    private Validation() { }

    public sealed record Valid(T Value) : Validation<T>;

    public sealed record Invalid : Validation<T>
    {
        public ImmutableArray<string> Reasons { get; }

        public Invalid(ImmutableArray<string> reasons) =>
            Reasons = reasons.IsDefaultOrEmpty
                ? throw new ArgumentException("Invalid needs at least one reason", nameof(reasons))
                : reasons;
    }
}
