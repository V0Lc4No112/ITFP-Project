using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rental.Domain.States;
using Rental.Domain.Results;

namespace Rental.App.Io;

public static class StockParser
{
    public const string Header = "ItemCode,Available";

    public static Validation<Stock> Parse(
        IReadOnlyList<string>? lines)
    {
        if (lines is null)
        {
            return new Validation<Stock>.Invalid(
                ImmutableArray.Create("Stock input is missing"));
        }

        if (lines.Count == 0 || lines[0] != Header)
        {
            return new Validation<Stock>.Invalid(
                ImmutableArray.Create("Row 1: Invalid stock header"));
        }

        var errors = ImmutableArray.CreateBuilder<string>();

        var rows = new List<(string ItemCode, int Available)>();

        for (int i = 1; i < lines.Count; i++)
        {
            int rowNumber = i + 1;
            string[] fields = lines[i].Split(',');

            if (fields.Length != 2)
            {
                errors.Add(
                    $"Row {rowNumber}: Expected 2 columns, found {fields.Length}");
                continue;
            }

            string itemCode = fields[0].Trim();

            if (!int.TryParse(fields[1].Trim(), out int available))
            {
                errors.Add(
                    $"Row {rowNumber}: Available must be an integer (was {fields[1]})");
                continue;
            }

            rows.Add((itemCode, available));
        }

        if (errors.Count > 0)
        {
            return new Validation<Stock>.Invalid(
                errors.ToImmutable());
        }

        return Stock.Create(rows);
    }
    
    public static Result<Stock> Load(string path) =>
    DataFile.ReadLines("stock", path)
        .Bind(lines => Parse(lines).ToResult(reasons =>
            new Error(
                ErrorCode.MalformedFile,
                $"Stock file '{path}' is malformed; no stock was loaded",
                reasons
            )
        ));

    
}
