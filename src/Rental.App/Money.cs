using System.Globalization;
using Rental.Domain.States;

namespace Rental.App;

/// <summary>
/// Shared text formats for every screen (Member D). Pure: no Console, no files.
/// </summary>
public static class Money
{
    /// <summary>Integer cents as "123.45" (invariant culture, so the screen is the same on every PC).</summary>
    public static string Format(long cents) =>
        (cents / 100).ToString(CultureInfo.InvariantCulture) + "." +
        (Math.Abs(cents) % 100).ToString("00", CultureInfo.InvariantCulture);

    /// <summary>"BIKE-01=0, SKI-02=0, TENT-01=1"; <c>stock.Items</c> is already sorted by item code (ordinal).</summary>
    public static string FormatStock(Stock stock) =>
        string.Join(", ", stock.Items
            .Select(item => $"{item.Key}={item.Value.ToString(CultureInfo.InvariantCulture)}"));
}
