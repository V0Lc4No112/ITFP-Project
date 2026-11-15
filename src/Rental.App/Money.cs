using System.Globalization;
using Rental.Domain.States;

namespace Rental.App;

/// <summary>
/// Shared text formats for every screen (Member D). Pure: no Console, no files.
/// </summary>
public static class Money
{
    /// <summary>Integer cents as "123.45" (invariant culture, so the screen is the same on every PC).</summary>
    public static string Format(long cents)
    {
        // Work on the magnitude so -2 becomes "-0.02" (not "0.02"); ulong also handles long.MinValue.
        ulong abs = cents < 0 ? (ulong)(-(cents + 1)) + 1 : (ulong)cents;

        return (cents < 0 ? "-" : "") +
            (abs / 100).ToString(CultureInfo.InvariantCulture) + "." +
            (abs % 100).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>"BIKE-01=0, SKI-02=0, TENT-01=1"; <c>stock.Items</c> is already sorted by item code (ordinal).</summary>
    public static string FormatStock(Stock stock) =>
        string.Join(", ", stock.Items
            .Select(item => $"{item.Key}={item.Value.ToString(CultureInfo.InvariantCulture)}"));
}
