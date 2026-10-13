
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rental.Domain.Models;

namespace Rental.App.Io;

public sealed record ParseOutcome(
    ImmutableArray<RentalRequest> Valid,
    ImmutableArray<string> Errors
);

public static class RequestParser
{
    public const string Header =
        "Id,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember";

    public static ParseOutcome Parse(IReadOnlyList<string>? lines)
    {
        var valid = ImmutableArray.CreateBuilder<RentalRequest>();
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

            if (string.IsNullOrWhiteSpace(id))
                errors.Add($"Row {rowNumber}: Id must not be blank");

            if (string.IsNullOrWhiteSpace(customer))
                errors.Add($"Row {rowNumber}: Customer must not be blank");

            if (string.IsNullOrWhiteSpace(itemCode))
                errors.Add($"Row {rowNumber}: ItemCode must not be blank");

            bool categoryValid = Enum.TryParse<Category>(
                fields[3].Trim(),
                ignoreCase: true,
                out Category category
            ) && Enum.IsDefined(category);

            if (!categoryValid)
                errors.Add($"Row {rowNumber}: Category is invalid (was {fields[3]})");

            bool rateValid = long.TryParse(fields[4].Trim(), out long dailyRate);

            if (!rateValid)
                errors.Add($"Row {rowNumber}: DailyRateCents must be a number (was {fields[4]})");
            else if (dailyRate <= 0)
                errors.Add($"Row {rowNumber}: DailyRateCents must be > 0 (was {dailyRate})");

            bool quantityValid = int.TryParse(fields[5].Trim(), out int quantity);

            if (!quantityValid)
                errors.Add($"Row {rowNumber}: Quantity must be an integer (was {fields[5]})");
            else if (quantity < 1)
                errors.Add($"Row {rowNumber}: Quantity must be >= 1 (was {quantity})");

            bool daysValid = int.TryParse(fields[6].Trim(), out int days);

            if (!daysValid)
                errors.Add($"Row {rowNumber}: Days must be an integer (was {fields[6]})");
            else if (days < 1)
                errors.Add($"Row {rowNumber}: Days must be >= 1 (was {days})");

            bool memberValid = bool.TryParse(fields[7].Trim(), out bool isMember);

            if (!memberValid)
                errors.Add($"Row {rowNumber}: IsMember must be true or false (was {fields[7]})");

            if (errors.Count == errorCountBefore)
            {
                valid.Add(new RentalRequest(
                    id,
                    customer,
                    itemCode,
                    category,
                    dailyRate,
                    quantity,
                    days,
                    isMember
                ));
            }
        }

        return new ParseOutcome(valid.ToImmutable(), errors.ToImmutable());
    }
}
