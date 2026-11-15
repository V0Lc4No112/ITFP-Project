using Rental.Domain.Models;
using System.Collections.Immutable;

namespace Rental.Domain.States;

public sealed class ValidatedRental
{
    /// <summary>
    /// Business limits for one request: 10,000.00 per day, 1,000 items, 365 days. They keep
    /// DailyRateCents × Quantity × Days below 3.65e12 cents, far from long overflow.
    /// </summary>
    public const long MaxDailyRateCents = 1_000_000;
    public const int MaxQuantity = 1_000;
    public const int MaxDays = 365;

    public RentalRequest Request { get; }

    private ValidatedRental(RentalRequest request)
    {
        Request = request;
    }

    public static Validation<ValidatedRental> Create(
        RentalRequest candidate)
    {
        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(candidate.Id))
            reasons.Add("Id must not be blank");

        if (string.IsNullOrWhiteSpace(candidate.Customer))
            reasons.Add("Customer must not be blank");

        if (string.IsNullOrWhiteSpace(candidate.ItemCode))
            reasons.Add("ItemCode must not be blank");

        if (!Enum.IsDefined(candidate.Category))
            reasons.Add(
                $"Category is invalid (was {(int)candidate.Category})");

        if (candidate.DailyRateCents <= 0)
            reasons.Add(
                $"DailyRateCents must be > 0 (was {candidate.DailyRateCents})");
        else if (candidate.DailyRateCents > MaxDailyRateCents)
            reasons.Add(
                $"DailyRateCents must be <= {MaxDailyRateCents} (was {candidate.DailyRateCents})");

        if (candidate.Quantity < 1)
            reasons.Add(
                $"Quantity must be >= 1 (was {candidate.Quantity})");
        else if (candidate.Quantity > MaxQuantity)
            reasons.Add(
                $"Quantity must be <= {MaxQuantity} (was {candidate.Quantity})");

        if (candidate.Days < 1)
            reasons.Add(
                $"Days must be >= 1 (was {candidate.Days})");
        else if (candidate.Days > MaxDays)
            reasons.Add(
                $"Days must be <= {MaxDays} (was {candidate.Days})");

        if (reasons.Count > 0)
            return new Validation<ValidatedRental>.Invalid(
                reasons.ToImmutableArray());

        return new Validation<ValidatedRental>.Valid(
            new ValidatedRental(candidate));
    }
}