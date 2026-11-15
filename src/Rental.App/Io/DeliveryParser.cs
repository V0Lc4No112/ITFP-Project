using System.Collections.Immutable;
using System.Globalization;
using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.App.Io;

public static class DeliveryParser
{
    public const string Header = "ItemCode,Added";

    public static Validation<ImmutableArray<Delivery>> Parse(
        IReadOnlyList<string>? lines)
    {
        var deliveries = ImmutableArray.CreateBuilder<Delivery>();
        var errors = ImmutableArray.CreateBuilder<string>();

        if (lines is null || lines.Count == 0 || lines[0] != Header)
            return new Validation<ImmutableArray<Delivery>>.Invalid(
                ImmutableArray.Create("Invalid deliveries header"));

        for (int i = 1; i < lines.Count; i++)
        {
            int row = i + 1;
            string[] fields = lines[i].Split(',');

            if (fields.Length != 2)
            {
                errors.Add($"Row {row}: Expected 2 columns, found {fields.Length}");
                continue;
            }

            string itemCode = fields[0].Trim();

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                errors.Add($"Row {row}: ItemCode must not be blank");
                continue;
            }

            if (!int.TryParse(fields[1].Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int added))
            {
                errors.Add($"Row {row}: Added must be an integer (was {fields[1]})");
                continue;
            }


            deliveries.Add(new Delivery(itemCode, added));
        }

        return errors.Count > 0
            ? new Validation<ImmutableArray<Delivery>>.Invalid(errors.ToImmutable())
            : new Validation<ImmutableArray<Delivery>>.Valid(deliveries.ToImmutable());
    }

    public static Result<ImmutableArray<Delivery>> Load(string path) =>
        DataFile.ReadLines("deliveries", path)
            .Bind(lines => Parse(lines).ToResult(reasons =>
                new Error(ErrorCode.MalformedFile,
                    $"Deliveries file '{path}' is malformed",
                    reasons)));
}
