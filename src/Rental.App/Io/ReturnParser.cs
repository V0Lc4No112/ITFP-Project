using System.Collections.Immutable;
using System.Globalization;
using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.App.Io;

/// <summary>
/// returns.csv → <see cref="ReturnEvent"/> rows (Member C). Text checks only; impossible numbers
/// (0 days, too many damaged) are rejected later by the domain rule, line by line.
/// </summary>
public static class ReturnParser
{
    public const string Header = "BookingId,DaysUsed,Damaged";

    public static Validation<ImmutableArray<ReturnEvent>> Parse(IReadOnlyList<string>? lines)
    {
        if (lines is null || lines.Count == 0 || lines[0] != Header)
        {
            return new Validation<ImmutableArray<ReturnEvent>>.Invalid(["Row 1: Invalid returns header"]);
        }

        var events = ImmutableArray.CreateBuilder<ReturnEvent>();
        var errors = ImmutableArray.CreateBuilder<string>();

        for (int i = 1; i < lines.Count; i++)
        {
            int row = i + 1;
            string[] fields = lines[i].Split(',');

            if (fields.Length != 3)
            {
                errors.Add($"Row {row}: Expected 3 columns, found {fields.Length}");
                continue;
            }

            string bookingId = fields[0].Trim();
            if (bookingId.Length == 0)
            {
                errors.Add($"Row {row}: BookingId must not be blank");
                continue;
            }

            if (!int.TryParse(fields[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int daysUsed))
            {
                errors.Add($"Row {row}: DaysUsed must be an integer (was {fields[1]})");
                continue;
            }

            if (!int.TryParse(fields[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int damaged))
            {
                errors.Add($"Row {row}: Damaged must be an integer (was {fields[2]})");
                continue;
            }

            events.Add(new ReturnEvent(bookingId, daysUsed, damaged));
        }

        return errors.Count > 0
            ? new Validation<ImmutableArray<ReturnEvent>>.Invalid(errors.ToImmutable())
            : new Validation<ImmutableArray<ReturnEvent>>.Valid(events.ToImmutable());
    }

    public static Result<ImmutableArray<ReturnEvent>> Load(string path) =>
        DataFile.ReadLines("returns", path)
            .Bind(lines => Parse(lines).ToResult(reasons => new Error(
                ErrorCode.MalformedFile,
                $"Returns file '{path}' is malformed; nothing was returned",
                reasons)));
}
