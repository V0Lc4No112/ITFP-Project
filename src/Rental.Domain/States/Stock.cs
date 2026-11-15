using System.Collections.Immutable;
using Rental.Domain.Results;

namespace Rental.Domain.States;

public sealed class Stock
{
    private readonly ImmutableDictionary<string, int> _available;

    /// <summary>Business limit per item; keeps every stock change far from int overflow.</summary>
    public const int MaxPerItem = 1_000_000;

    private Stock(ImmutableDictionary<string, int> available) => _available = available;

    public static Validation<Stock> Create(IReadOnlyList<(string ItemCode, int Available)> rows) =>
        Problems(rows) switch
        {
            [] => new Validation<Stock>.Valid(
                new Stock(rows.ToImmutableDictionary(r => r.ItemCode, r => r.Available, StringComparer.Ordinal))),
            var reasons => new Validation<Stock>.Invalid(reasons),
        };

    public Option<int> Available(string itemCode) =>
        _available.TryGetValue(itemCode, out var available)
            ? Option.Some(available)
            : Option.None<int>();

    internal Stock Reserve(string itemCode, int quantity) =>
        new(_available.SetItem(itemCode, checked(_available[itemCode] - quantity)));

    internal Stock Release(string itemCode, int quantity) =>
        new(_available.SetItem(itemCode, checked(_available[itemCode] + quantity)));

    public IEnumerable<KeyValuePair<string, int>> Items =>
        _available.OrderBy(item => item.Key, StringComparer.Ordinal);

    private static ImmutableArray<string> Problems(IReadOnlyList<(string ItemCode, int Available)> rows) =>
        rows
            .Where(r => string.IsNullOrWhiteSpace(r.ItemCode))
            .Select(_ => "ItemCode must not be blank")
            .Concat(rows
                .Where(r => !string.IsNullOrWhiteSpace(r.ItemCode))
                .GroupBy(r => r.ItemCode, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => $"ItemCode {g.Key} is listed twice"))
            .Concat(rows
                .Where(r => r.Available < 0)
                .Select(r => $"Available for {r.ItemCode} must be >= 0 (was {r.Available})"))
            .Concat(rows
                .Where(r => r.Available > MaxPerItem)
                .Select(r => $"Available for {r.ItemCode} must be <= {MaxPerItem} (was {r.Available})"))
            .ToImmutableArray();
}
