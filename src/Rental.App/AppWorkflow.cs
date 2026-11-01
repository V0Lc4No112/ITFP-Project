using System.Collections.Immutable;
using Rental.App.Io;
using Rental.App.Native;
using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.Reports;
using Rental.Domain.Results;

namespace Rental.App;

public sealed record RunOutcome(
    string PolicyName,
    QuoteSummary Summary,
    BookingReport Booking,
    long ServiceFeeCents,
    ImmutableArray<CategoryRevenue> Revenue,
    ImmutableArray<LowStockLine> LowStock)
{
    /// <summary>Confirmed bookings plus the fee computed by the native C library.</summary>
    public long TotalDueCents => Booking.ConfirmedTotalCents + ServiceFeeCents;

    /// <summary>Where the revenue report was saved, when the run exported it.</summary>
    public Option<string> ExportedTo { get; init; } = Option.None<string>();
}

public static class AppWorkflow
{
    private const long ServiceFeePerItemCents = 100;

    /// <summary>Report 2 lists items with at most this many left after booking.</summary>
    public const int LowStockThreshold = 1;

    /// <summary>
    /// The one chain: load requests, pick the policy, run the A5 pipeline, load stock, confirm (Map: cannot fail),
    /// native fee (Bind: can fail), then the outcome with both reports computed once (Map).
    /// </summary>
    public static Result<RunOutcome> Execute(string requestsPath, string policyName, string stockPath) =>
        RequestParser.Load(requestsPath)
            .Bind(rentals => PricingPolicies.FromName(policyName)
                .ToResult(() => new Error(
                    ErrorCode.UnknownPolicy,
                    $"Unknown policy '{policyName}'. Use 'standard', 'discount' or 'weekly'"))
                .Map(policy => RentalWorkflow.Run(rentals, policy, RentalWorkflow.DefaultMaxDays)))
            .Bind(summary => StockParser.Load(stockPath)
                .Map(stock => Booking.ConfirmAll(summary, stock))
                .Bind(booking => ServiceFee(booking)
                    .Map(fee => new RunOutcome(
                        policyName.ToLowerInvariant(),
                        summary,
                        booking,
                        fee,
                        RentalReports.RevenueByCategory(booking),
                        RentalReports.LowStock(booking.RemainingStock, LowStockThreshold)))));

    /// <summary>
    /// <see cref="Execute"/>, then saves report 1 as CSV. If saving fails, the whole run fails and nothing is
    /// printed; the report rows are built before the file is opened.
    /// </summary>
    public static Result<RunOutcome> ExecuteAndExport(
        string requestsPath, string policyName, string stockPath, string exportPath) =>
        Execute(requestsPath, policyName, stockPath)
            .Bind(outcome => DataFile.WriteLines("report", exportPath, ReportCsv.Revenue(outcome.Revenue))
                .Map(_ => outcome with { ExportedTo = Option.Some(exportPath) }));

    /// <summary>The native fee for every confirmed item (A's adapter, B's ConfirmedQuotes extension).</summary>
    private static Result<long> ServiceFee(BookingReport booking)
    {
        long quantity = booking.ConfirmedQuotes().Sum(quote => (long)quote.Request.Quantity);

        return quantity > int.MaxValue
            ? Result.Fail<long>(new Error(ErrorCode.InvalidRequest, "Too many items for native fee calculation"))
            : NativeFeeAdapter.Calculate((int)quantity, ServiceFeePerItemCents);
    }
}
