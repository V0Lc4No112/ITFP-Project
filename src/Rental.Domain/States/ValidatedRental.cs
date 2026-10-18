using Rental.Domain.Models;

namespace Rental.Domain.States;

public sealed class ValidatedRental
{
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

        if (candidate.Quantity < 1)
            reasons.Add(
                $"Quantity must be >= 1 (was {candidate.Quantity})");

        if (candidate.Days < 1)
            reasons.Add(
                $"Days must be >= 1 (was {candidate.Days})");

        if (reasons.Count > 0)
            return new Validation<ValidatedRental>.Invalid(reasons);

        return new Validation<ValidatedRental>.Valid(
            new ValidatedRental(candidate));
    }
}