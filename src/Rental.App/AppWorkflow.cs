using Rental.App.Io;
using Rental.App.Native;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.App;

public sealed record RunOutcome(
    string PolicyName,
    QuoteSummary Summary,
    BookingReport Booking,
    long ServiceFeeCents);

public static class AppWorkflow
{
    private const long ServiceFeePerItemCents = 100;

    public static Result<RunOutcome> Execute(
        string requestsPath,
        string policyName,
        string stockPath) =>

        RequestParser.Load(requestsPath)
            .Bind(rentals =>
                PricingPolicies.FromName(policyName)
                    .ToResult(() => new Error(
                        ErrorCode.UnknownPolicy,
                        $"Unknown policy '{policyName}'. Use 'standard', 'discount' or 'weekly'"))
                    .Map(policy =>
                        RentalWorkflow.Run(
                            rentals,
                            policy,
                            RentalWorkflow.DefaultMaxDays)))

            .Bind(summary =>
                StockParser.Load(stockPath)
                    .Bind(stock =>
                    {
                        var booking = Booking.ConfirmAll(summary, stock);

                        long quantity = booking.Lines
                            .Where(line =>
                                line.Result is Result<Reservation>.Ok)
                            .Sum(line =>
                                (long)line.Quote.Request.Quantity);

                        if (quantity > int.MaxValue)
                        {
                            return Result.Fail<RunOutcome>(
                                new Error(
                                    ErrorCode.InvalidRequest,
                                    "Too many items for native fee calculation"));
                        }

                        return NativeFeeAdapter.Calculate(
                                (int)quantity,
                                ServiceFeePerItemCents)
                            .Map(fee => new RunOutcome(
                                policyName.ToLowerInvariant(),
                                summary,
                                booking,
                                fee));
                    }));
}
