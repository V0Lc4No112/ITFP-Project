using Rental.Domain.Models;

namespace Rental.Domain.Pipeline;

public static class RentalWorkflow
{
    public const int DefaultMaxDays = 14;

    /// <summary>
    /// Composition (Member D): SelectEligible, then PriceAll, then BuildSummary.
    /// Each stage's output is the next stage's input; nothing is printed or stored here.
    /// </summary>
    public static QuoteSummary Run(IReadOnlyList<RentalRequest> requests, Func<RentalRequest, long> policy, int maxDays)
    {
        var eligible = Stages.SelectEligible(requests, maxDays);
        var priced = Pricing.PriceAll(eligible, policy);
        return Stages.BuildSummary(priced);
    }
}
