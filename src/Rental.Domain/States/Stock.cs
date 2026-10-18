using System.Collections.Immutable;

namespace Rental.Domain.States;

public sealed class Stock
{
    private readonly ImmutableDictionary<string, int> _available;

    private Stock(ImmutableDictionary<string, int> available) => _available = available;

    public static Validation<Stock> Create(IReadOnlyList<(string ItemCode, int Available)> rows) =>
        Problems(rows) switch
        {
            [] => new Validation<Stock>.Valid(
                new Stock(rows.ToImmutableDictionary(r => r.ItemCode, r => r.Available, StringComparer.Ordinal))),
            var reasons => new Validation<Stock>.Invalid(reasons),
        };

    public int? Available(string itemCode) =>
        _available.TryGetValue(itemCode, out var available) ? available : null;

    internal Stock Reserve(string itemCode, int quantity) =>
        new(_available.SetItem(itemCode, _available[itemCode] - quantity));

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
            .ToImmutableArray();
}
