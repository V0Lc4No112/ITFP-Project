using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Rental.Domain.Models;
using Rental.Domain.States;
using Rental.Domain.Results;
using Rental.Domain.Extensions;

namespace Rental.App.Io;

public static class BookingFile
{
    public const string Header =
        "BookingId,RequestId,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember,TotalCents,Status";

    public static Validation<ImmutableArray<BookingState>> Parse(
        IReadOnlyList<string>? lines)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        var bookings = ImmutableArray.CreateBuilder<BookingState>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        if (lines is null)
        {
            return new Validation<ImmutableArray<BookingState>>.Invalid(
                ImmutableArray.Create("Booking input is missing"));
        }

        if (lines.Count == 0 || lines[0] != Header)
        {
            return new Validation<ImmutableArray<BookingState>>.Invalid(
                ImmutableArray.Create("Row 1: Invalid booking header"));
        }

        for (int i = 1; i < lines.Count; i++)
        {
            int rowNumber = i + 1;
            string[] fields = lines[i].Split(',');

            if (fields.Length != 11)
            {
                errors.Add(
                    $"Row {rowNumber}: Expected 11 columns, found {fields.Length}");
                continue;
            }

            var rowErrors = new List<string>();

            string bookingId = fields[0].Trim();
            string requestId = fields[1].Trim();
            string customer = fields[2].Trim();
            string itemCode = fields[3].Trim();
            string categoryText = fields[4].Trim();
            string status = fields[10].Trim();

            if (string.IsNullOrWhiteSpace(bookingId))
            {
                rowErrors.Add("BookingId must not be blank");
            }
            else if (!seenIds.Add(bookingId))
            {
                rowErrors.Add($"BookingId {bookingId} is duplicated");
            }

            bool categoryValid =
                Enum.TryParse<Category>(
                    categoryText,
                    ignoreCase: true,
                    out Category category)
                && Enum.IsDefined(category)
                && !int.TryParse(categoryText, out _);

            if (!categoryValid)
                rowErrors.Add($"Category is invalid (was {fields[4]})");

            bool rateValid = long.TryParse(
                fields[5].Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long dailyRate);

            if (!rateValid)
                rowErrors.Add(
                    $"DailyRateCents must be a number (was {fields[5]})");

            bool quantityValid = int.TryParse(
                fields[6].Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int quantity);

            if (!quantityValid)
                rowErrors.Add(
                    $"Quantity must be an integer (was {fields[6]})");

            bool daysValid = int.TryParse(
                fields[7].Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int days);

            if (!daysValid)
                rowErrors.Add(
                    $"Days must be an integer (was {fields[7]})");

            bool memberValid = bool.TryParse(
                fields[8].Trim(),
                out bool isMember);

            if (!memberValid)
                rowErrors.Add(
                    $"IsMember must be true or false (was {fields[8]})");

            bool totalValid = long.TryParse(
                fields[9].Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long totalCents);

            if (!totalValid)
                rowErrors.Add(
                    $"TotalCents must be a number (was {fields[9]})");
            else if (totalCents < 0)
                rowErrors.Add(
                    $"TotalCents must be >= 0 (was {totalCents})");

            if (status is not ("Confirmed" or "Returned" or "Cancelled"))
                rowErrors.Add($"Unknown booking status (was {status})");

            if (rowErrors.Count > 0)
            {
                foreach (string error in rowErrors)
                    errors.Add($"Row {rowNumber}: {error}");

                continue;
            }

            var request = new RentalRequest(
                requestId,
                customer,
                itemCode,
                category,
                dailyRate,
                quantity,
                days,
                isMember);

            switch (ValidatedRental.Create(request))
            {
                case Validation<ValidatedRental>.Invalid invalid:
                    foreach (string reason in invalid.Reasons)
                        errors.Add($"Row {rowNumber}: {reason}");
                    continue;

                case Validation<ValidatedRental>.Valid valid:
                    var quote = new PricedRental(
                        valid.Value.Request,
                        totalCents);

                    BookingState booking = status switch
                    {
                        "Confirmed" =>
                            new BookingState.Confirmed(quote, bookingId),

                        "Returned" =>
                            new BookingState.Returned(quote, bookingId),

                        "Cancelled" =>
                            new BookingState.Cancelled(quote, bookingId),

                        _ => throw new InvalidOperationException(
                            "Unexpected booking status")
                    };

                    bookings.Add(booking);
                    break;
            }
        }

        if (errors.Count > 0)
        {
            return new Validation<ImmutableArray<BookingState>>.Invalid(
                errors.ToImmutable());
        }

        return new Validation<ImmutableArray<BookingState>>.Valid(
            bookings.ToImmutable());
    }

    public static Result<ImmutableArray<BookingState>> Load(string path) =>
        DataFile.ReadLines("bookings", path)
            .Bind(lines => Parse(lines).ToResult(reasons =>
                new Error(
                    ErrorCode.MalformedFile,
                    $"Bookings file '{path}' is malformed; no bookings were loaded",
                    reasons)));

    public static ImmutableArray<string> Lines(
        IReadOnlyList<BookingState> bookings)
    {
        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add(Header);

        foreach (BookingState booking in bookings)
        {
            string status = booking switch
            {
                BookingState.Confirmed => "Confirmed",
                BookingState.Returned => "Returned",
                BookingState.Cancelled => "Cancelled",
                BookingState.Quoted => throw new ArgumentException(
                    "Quoted bookings cannot be saved",
                    nameof(bookings)),
                _ => throw new InvalidOperationException(
                    "Unknown booking state")
            };

            PricedRental quote = booking.QuoteOf();
            RentalRequest request = quote.Request;

            lines.Add(string.Join(",",
                booking.IdOf(),
                request.Id,
                request.Customer,
                request.ItemCode,
                request.Category,
                request.DailyRateCents.ToString(CultureInfo.InvariantCulture),
                request.Quantity.ToString(CultureInfo.InvariantCulture),
                request.Days.ToString(CultureInfo.InvariantCulture),
                request.IsMember.ToString().ToLowerInvariant(),
                quote.TotalCents.ToString(CultureInfo.InvariantCulture),
                status));
        }

        return lines.ToImmutable();
    }
}
