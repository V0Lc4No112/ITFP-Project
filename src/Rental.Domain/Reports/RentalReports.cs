using System.Collections.Immutable;
using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.States;

namespace Rental.Domain.Reports;

public sealed record CategoryRevenue(Category Category, int Bookings, int Items, long RevenueCents);

public sealed record LowStockLine(string ItemCode, int Left);

public static class RentalReports
{
    public static ImmutableArray<CategoryRevenue> RevenueByCategory(BookingReport report) =>
        report.ConfirmedQuotes()
            .GroupBy(quote => quote.Request.Category)
            .Select(group => new CategoryRevenue(
                group.Key,
                group.Count(),
                group.Sum(quote => quote.Request.Quantity),
                group.Sum(quote => quote.TotalCents)))
            .OrderByDescending(row => row.RevenueCents)
            .ThenBy(row => row.Category.ToString(), StringComparer.Ordinal)
            .ToImmutableArray();

    public static ImmutableArray<LowStockLine> LowStock(Stock stock, int threshold) =>
        stock.Items
            .Where(item => item.Value <= threshold)
            .Select(item => new LowStockLine(item.Key, item.Value))
            .OrderBy(row => row.Left)
            .ThenBy(row => row.ItemCode, StringComparer.Ordinal)
            .ToImmutableArray();
}
