using Rental.Domain.Models;
using Rental.Domain.States;

namespace Rental.Domain.Pipeline;

public static class RentalWorkflow
{
    public const int DefaultMaxDays = 14;

    /// <summary>
    /// Composition (Member D): SelectEligible, then PriceAll, then BuildSummary.
    /// Each stage's output is the next stage's input; nothing is printed or stored here.
    /// Only <see cref="ValidatedRental"/> values are accepted, so a request that skipped
    /// <see cref="ValidatedRental.Create"/> cannot be priced.
    /// </summary>
    public static QuoteSummary Run(IReadOnlyList<ValidatedRental> rentals, Func<RentalRequest, long> policy, int maxDays)
    {
        var requests = rentals.Select(r => r.Request).ToList();
        var eligible = Stages.SelectEligible(requests, maxDays);
        var priced = Pricing.PriceAll(eligible, policy);
        return Stages.BuildSummary(priced);
    }
}
