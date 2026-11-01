using Rental.App.Io;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.Results;

namespace Rental.App;

public sealed record RunOutcome(string PolicyName, QuoteSummary Summary, BookingReport Booking);

public static class AppWorkflow
{
    /// <summary>
    /// The one real chain (Member D): requests → policy → pricing → stock → booking.
    /// <c>Bind</c> where the next step can fail (it returns a <see cref="Result{T}"/>),
    /// <c>Map</c> where it cannot (it returns a plain value). The first failure skips every later step,
    /// so an invalid requests file is reported even when the policy and stock are also wrong.
    /// </summary>
    public static Result<RunOutcome> Execute(string requestsPath, string policyName, string stockPath) =>
        RequestParser.Load(requestsPath)                                           // Result<ImmutableArray<ValidatedRental>>
            .Bind(rentals => PricingPolicies.FromName(policyName)                  // Option<Func<...>>: can be missing
                .ToResult(() => new Error(ErrorCode.UnknownPolicy,
                    $"Unknown policy '{policyName}'. Use 'standard' or 'discount'"))
                .Map(policy => RentalWorkflow.Run(rentals, policy, RentalWorkflow.DefaultMaxDays)))   // cannot fail
            .Bind(summary => StockParser.Load(stockPath)                           // can fail: missing or malformed file
                .Map(stock => new RunOutcome(policyName.ToLowerInvariant(), summary, Booking.ConfirmAll(summary, stock))));
}
