using System.Collections.Immutable;
using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.App;

/// <summary>
/// Turns outcomes into screen lines. Pure: it returns lines and never writes,
/// so <c>Program</c> prints only after the whole chain has finished and the screen can be tested.
/// </summary>
public static class Display
{
    public static ImmutableArray<string> Render(RunOutcome outcome)
    {
        var report = outcome.Booking;
        var lines = ImmutableArray.CreateBuilder<string>();

        lines.AddRange(RenderQuote(outcome.PolicyName, outcome.Summary));
        lines.Add("");
        lines.Add("Bookings:");
        foreach (var line in report.Lines)
        {
            lines.Add(line.Result switch
            {
                Result<Reservation>.Ok ok =>
                    $"{ok.Value.Booking.BookingId,-6} confirmed  {Money.Format(line.Quote.TotalCents),10}",
                Result<Reservation>.Fail fail =>
                    $"{line.Quote.Request.Id,-6} REJECTED   [{fail.Error.Code}] {fail.Error.Message}",
                _ => throw new InvalidOperationException("Unknown confirm result"),
            });
        }
        lines.Add($"Confirmed total: {Money.Format(report.ConfirmedTotalCents)}");
        lines.Add($"Service fee (native C): {Money.Format(outcome.ServiceFeeCents)}");
        lines.Add($"Total due: {Money.Format(outcome.TotalDueCents)}");
        lines.Add($"Stock left: {Money.FormatStock(report.RemainingStock)}");
        lines.Add("");
        lines.Add("Revenue by category:");
        if (outcome.Revenue.IsEmpty)
        {
            lines.Add("No confirmed bookings.");
        }
        foreach (var row in outcome.Revenue)
        {
            lines.Add($"{row.Category,-8} {row.Bookings,3} booking(s) {row.Items,3} item(s) {Money.Format(row.RevenueCents),10}");
        }
        lines.Add("");
        lines.Add($"Low stock (left <= {AppWorkflow.LowStockThreshold}):");
        if (outcome.LowStock.IsEmpty)
        {
            lines.Add("No item is low.");
        }
        foreach (var row in outcome.LowStock)
        {
            lines.Add($"{row.ItemCode,-8} {row.Left,3} left");
        }
        if (outcome.ExportedTo is Option<string>.Some exported)
        {
            lines.Add("");
            lines.Add($"Revenue report saved to {exported.Value}");
        }
        if (outcome.SavedTo is Option<string>.Some saved)
        {
            lines.Add("");
            lines.Add($"Bookings and stock saved to {saved.Value}");
        }

        return lines.ToImmutable();
    }

    /// <summary>The quote part of the screen; used alone by the <c>quote</c> command (W1).</summary>
    public static ImmutableArray<string> RenderQuote(string policyName, QuoteSummary summary)
    {
        var lines = ImmutableArray.CreateBuilder<string>();

        lines.Add($"Policy: {policyName}");
        if (summary.Count == 0)
        {
            lines.Add("No eligible rentals.");
        }
        foreach (var quote in summary.Quotes)
        {
            lines.Add($"{quote.Request.Id,-4} {quote.Request.Customer,-10} {Money.Format(quote.TotalCents),10}");
        }
        lines.Add($"Grand total: {Money.Format(summary.GrandTotalCents)}");
        lines.Add($"Count: {summary.Count}");

        return lines.ToImmutable();
    }

    public static ImmutableArray<string> RenderError(Error error) =>
        [
            $"{(error.IsOperational ? "File problem" : "Rejected")} [{error.Code}]: {error.Message}",
            .. error.Details.Select(detail => $"  {detail}"),
        ];
}
