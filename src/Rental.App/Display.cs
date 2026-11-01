using System.Collections.Immutable;
using System.Globalization;
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
        var summary = outcome.Summary;
        var report = outcome.Booking;
        var lines = ImmutableArray.CreateBuilder<string>();

        lines.Add($"Policy: {outcome.PolicyName}");
        if (summary.Count == 0)
        {
            lines.Add("No eligible rentals.");
        }
        foreach (var quote in summary.Quotes)
        {
            lines.Add($"{quote.Request.Id,-4} {quote.Request.Customer,-10} {FormatCents(quote.TotalCents),10}");
        }
        lines.Add($"Grand total: {FormatCents(summary.GrandTotalCents)}");
        lines.Add($"Count: {summary.Count}");
        lines.Add("");
        lines.Add("Bookings:");
        foreach (var line in report.Lines)
        {
            lines.Add(line.Result switch
            {
                Result<Reservation>.Ok ok =>
                    $"{ok.Value.Booking.BookingId,-6} confirmed  {FormatCents(line.Quote.TotalCents),10}",
                Result<Reservation>.Fail fail =>
                    $"{line.Quote.Request.Id,-6} REJECTED   [{fail.Error.Code}] {fail.Error.Message}",
                _ => throw new InvalidOperationException("Unknown confirm result"),
            });
        }
        lines.Add($"Confirmed total: {FormatCents(report.ConfirmedTotalCents)}");
        lines.Add($"Service fee (native C): {FormatCents(outcome.ServiceFeeCents)}");
        lines.Add($"Total due: {FormatCents(outcome.TotalDueCents)}");
        lines.Add($"Stock left: {FormatStock(report.RemainingStock)}");
        lines.Add("");
        lines.Add("Revenue by category:");
        if (outcome.Revenue.IsEmpty)
        {
            lines.Add("No confirmed bookings.");
        }
        foreach (var row in outcome.Revenue)
        {
            lines.Add($"{row.Category,-8} {row.Bookings,3} booking(s) {row.Items,3} item(s) {FormatCents(row.RevenueCents),10}");
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

        return lines.ToImmutable();
    }

    public static ImmutableArray<string> RenderError(Error error) =>
        [
            $"{(error.IsOperational ? "File problem" : "Rejected")} [{error.Code}]: {error.Message}",
            .. error.Details.Select(detail => $"  {detail}"),
        ];

    private static string FormatCents(long cents) =>
        (cents / 100).ToString(CultureInfo.InvariantCulture) + "." +
        (Math.Abs(cents) % 100).ToString("00", CultureInfo.InvariantCulture);

    // stock.Items is already sorted by item code (ordinal).
    private static string FormatStock(Stock stock) =>
        string.Join(", ", stock.Items
            .Select(item => $"{item.Key}={item.Value.ToString(CultureInfo.InvariantCulture)}"));
}
