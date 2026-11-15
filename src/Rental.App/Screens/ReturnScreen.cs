using System.Collections.Immutable;
using Rental.App.Workflows;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.App.Screens;

/// <summary>Screen lines for W5 return (Member C). Pure.</summary>
public static class ReturnScreen
{
    public static ImmutableArray<string> Render(ReturnOutcome outcome)
    {
        var report = outcome.Report;
        var lines = ImmutableArray.CreateBuilder<string>();

        lines.Add("Returns:");
        if (report.Lines.IsEmpty)
        {
            lines.Add("No returns.");
        }
        foreach (var line in report.Lines)
        {
            lines.Add(line.Result switch
            {
                Result<ReturnResult>.Ok ok =>
                    $"{line.Event.BookingId,-6} returned   late {ok.Value.LateDays} day(s), damaged {ok.Value.Damaged}, extra {Money.Format(ok.Value.FeeCents),8}",
                Result<ReturnResult>.Fail fail =>
                    $"{line.Event.BookingId,-6} REJECTED   [{fail.Error.Code}] {fail.Error.Message}",
                _ => throw new InvalidOperationException("Unknown return result"),
            });
        }
        lines.Add($"Extra charges: {Money.Format(report.ExtraChargesCents)}");
        lines.Add($"Stock now: {Money.FormatStock(report.Stock)}");
        if (outcome.SavedTo is Option<string>.Some saved)
        {
            lines.Add("");
            lines.Add($"Bookings and stock saved to {saved.Value}");
        }

        return lines.ToImmutable();
    }
}
