using System.Collections.Immutable;
using System.Globalization;
using Rental.Domain.Reports;

namespace Rental.App;

/// <summary>
/// Pure: turns report 1 into CSV lines. Writing them is A's <c>DataFile.WriteLines</c>.
/// Money stays in integer cents, so nothing is rounded and the file can be read back exactly.
/// </summary>
public static class ReportCsv
{
    public const string RevenueHeader = "Category,Bookings,Items,RevenueCents";

    public static ImmutableArray<string> Revenue(ImmutableArray<CategoryRevenue> rows) =>
        [
            RevenueHeader,
            .. rows.Select(row => string.Join(",",
                row.Category,
                row.Bookings.ToString(CultureInfo.InvariantCulture),
                row.Items.ToString(CultureInfo.InvariantCulture),
                row.RevenueCents.ToString(CultureInfo.InvariantCulture))),
        ];
}
