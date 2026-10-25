using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rental.Domain.Models;
using Rental.Domain.States;
using Rental.Domain.Results;


namespace Rental.App.Io;

public sealed record ParseOutcome(
    ImmutableArray<ValidatedRental> Valid,
    ImmutableArray<string> Errors
);

public static class RequestParser
{
    public const string Header =
        "Id,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember";

    public static ParseOutcome Parse(IReadOnlyList<string>? lines)
    {
        var valid = ImmutableArray.CreateBuilder<ValidatedRental>();
        var errors = ImmutableArray.CreateBuilder<string>();

        if (lines is null)
        {
            errors.Add("Input is missing");
            return new ParseOutcome(valid.ToImmutable(), errors.ToImmutable());
        }

        if (lines.Count == 0)
        {
            errors.Add("Row 1: Header is missing");
            return new ParseOutcome(valid.ToImmutable(), errors.ToImmutable());
        }

        if (lines[0] != Header)
        {
            errors.Add("Row 1: Invalid header");
            return new ParseOutcome(valid.ToImmutable(), errors.ToImmutable());
        }

        for (int i = 1; i < lines.Count; i++)
        {
            int rowNumber = i + 1;
            string[] fields = lines[i].Split(',');

            if (fields.Length != 8)
            {
                errors.Add($"Row {rowNumber}: Expected 8 columns, found {fields.Length}");
                continue;
            }

            int errorCountBefore = errors.Count;

            string id = fields[0].Trim();
            string customer = fields[1].Trim();
            string itemCode = fields[2].Trim();

            string categoryText = fields[3].Trim();

            bool categoryValid =
                Enum.TryParse<Category>(
                    categoryText,
                    ignoreCase: true,
                    out Category category
                )
                && Enum.IsDefined(category)
                && !int.TryParse(categoryText, out _);

            if (!categoryValid)
            {
                errors.Add(
                    $"Row {rowNumber}: Category is invalid (was {fields[3]})"
                );
            }

            bool rateValid = long.TryParse(
                fields[4].Trim(),
                out long dailyRate
            );

            if (!rateValid)
                errors.Add(
                    $"Row {rowNumber}: DailyRateCents must be a number (was {fields[4]})"
                );

            bool quantityValid = int.TryParse(
                fields[5].Trim(),
                out int quantity
            );

            if (!quantityValid)
                errors.Add(
                    $"Row {rowNumber}: Quantity must be an integer (was {fields[5]})"
                );

            bool daysValid = int.TryParse(
                fields[6].Trim(),
                out int days
            );

            if (!daysValid)
                errors.Add(
                    $"Row {rowNumber}: Days must be an integer (was {fields[6]})"
                );

            bool memberValid = bool.TryParse(
                fields[7].Trim(),
                out bool isMember
            );

            if (!memberValid)
                errors.Add(
                    $"Row {rowNumber}: IsMember must be true or false (was {fields[7]})"
                );

            
            if (errors.Count != errorCountBefore)
                continue;

            var candidate = new RentalRequest(
                id,
                customer,
                itemCode,
                category,
                dailyRate,
                quantity,
                days,
                isMember
            );

            
            switch (ValidatedRental.Create(candidate))
            {
                case Validation<ValidatedRental>.Valid ok:
                    valid.Add(ok.Value);
                    break;

                case Validation<ValidatedRental>.Invalid bad:
                    foreach (var reason in bad.Reasons)
                    {
                        errors.Add($"Row {rowNumber}: {reason}");
                    }
                    break;

                default:
                    throw new InvalidOperationException(
                        "Unexpected validation result"
                    );
            }
        }

        return new ParseOutcome(
            valid.ToImmutable(),
            errors.ToImmutable()
        );
    }
    
public static Result<ImmutableArray<ValidatedRental>> Load(string path) =>
    DataFile.ReadLines("requests", path)
        .Bind(lines => Parse(lines) switch
        {
            { Errors.IsEmpty: true } ok =>
                Result.Ok(ok.Valid),

            var bad =>
                Result.Fail<ImmutableArray<ValidatedRental>>(
                    new Error(
                        ErrorCode.InvalidRequest,
                        $"Requests file '{path}' has {bad.Errors.Length} error(s); nothing was priced or booked",
                        bad.Errors
                    )
                )
        });

}