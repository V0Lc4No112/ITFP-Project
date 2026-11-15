using System.Collections.Immutable;
using System.Globalization;
using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.App.Io;

/// <summary>
/// cancellations.csv → <see cref="CancelEvent"/> rows (Member D). Text checks only: the header, the
/// column count, a non-blank id and an integer. A negative number is well-formed text and is rejected
/// later by the domain rule (<c>Cancellation.RefundPercent</c>), line by line.
/// </summary>
public static class CancellationParser
{
    public const string Header = "BookingId,DaysBeforeStart";

    public static Validation<ImmutableArray<CancelEvent>> Parse(IReadOnlyList<string>? lines)
    {
        if (lines is null || lines.Count == 0 || lines[0] != Header)
        {
            return new Validation<ImmutableArray<CancelEvent>>.Invalid(
                ["Row 1: Invalid cancellations header"]);
        }

        var events = ImmutableArray.CreateBuilder<CancelEvent>();
        var errors = ImmutableArray.CreateBuilder<string>();

        for (int i = 1; i < lines.Count; i++)
        {
            int row = i + 1;
            string[] fields = lines[i].Split(',');

            if (fields.Length != 2)
            {
                errors.Add($"Row {row}: Expected 2 columns, found {fields.Length}");
                continue;
            }

            string bookingId = fields[0].Trim();
            if (bookingId.Length == 0)
            {
                errors.Add($"Row {row}: BookingId must not be blank");
                continue;
            }

            if (!int.TryParse(fields[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int days))
            {
                errors.Add($"Row {row}: DaysBeforeStart must be an integer (was {fields[1]})");
                continue;
            }

            events.Add(new CancelEvent(bookingId, days));
        }

        return errors.Count > 0
            ? new Validation<ImmutableArray<CancelEvent>>.Invalid(errors.ToImmutable())
            : new Validation<ImmutableArray<CancelEvent>>.Valid(events.ToImmutable());
    }

    public static Result<ImmutableArray<CancelEvent>> Load(string path) =>
        DataFile.ReadLines("cancellations", path)
            .Bind(lines => Parse(lines).ToResult(reasons => new Error(
                ErrorCode.MalformedFile,
                $"Cancellations file '{path}' is malformed; nothing was cancelled",
                reasons)));
}
