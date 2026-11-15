using System.Collections.Immutable;
using Rental.Domain.Reports;

namespace Rental.App.Screens;

public static class InvoiceScreen
{
    public static ImmutableArray<string> Render(Invoice invoice)
    {
        var lines = ImmutableArray.CreateBuilder<string>();

        lines.Add($"Invoice: {invoice.Customer}{(invoice.IsMember ? " (member)" : "")}");
        if (invoice.Lines.IsEmpty)
        {
            lines.Add("No billable bookings.");
        }
        foreach (var line in invoice.Lines)
        {
            lines.Add($"{line.BookingId,-6} {line.ItemCode,-8} {line.Quantity} x {line.Days,2} days {Money.Format(line.AmountCents),10}");
        }
        lines.Add($"Subtotal:        {Money.Format(invoice.SubtotalCents),10}");
        lines.Add($"Member discount: {"-" + Money.Format(invoice.DiscountCents),10}");
        lines.Add($"Total:           {Money.Format(invoice.TotalCents),10}");

        return lines.ToImmutable();
    }
}
