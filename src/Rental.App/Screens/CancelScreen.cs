using System.Collections.Immutable;
using Rental.App.Workflows;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.App.Screens;

/// <summary>Screen lines for W6 cancel (Member D). Pure: returns lines, never prints.</summary>
public static class CancelScreen
{
    public static ImmutableArray<string> Render(CancelOutcome outcome)
    {
        var report = outcome.Report;
        var lines = ImmutableArray.CreateBuilder<string>();

        lines.Add("Cancellations:");
        if (report.Lines.IsEmpty)
        {
            lines.Add("No cancellations.");
        }
        foreach (var line in report.Lines)
        {
            lines.Add(line.Result switch
            {
                Result<CancelResult>.Ok ok =>
                    $"{line.Event.BookingId,-6} cancelled  refund {ok.Value.RefundPercent,3}% {Money.Format(ok.Value.RefundCents),10}",
                Result<CancelResult>.Fail fail =>
                    $"{line.Event.BookingId,-6} REJECTED   [{fail.Error.Code}] {fail.Error.Message}",
                _ => throw new InvalidOperationException("Unknown cancel result"),
            });
        }
        lines.Add($"Refund total: {Money.Format(report.RefundCents)}");
        lines.Add($"Stock now: {Money.FormatStock(report.Stock)}");
        if (outcome.SavedTo is Option<string>.Some saved)
        {
            lines.Add("");
            lines.Add($"Bookings and stock saved to {saved.Value}");
        }

        return lines.ToImmutable();
    }
}
