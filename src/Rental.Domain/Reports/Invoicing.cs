using System.Collections.Immutable;
using Rental.Domain.Extensions;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Domain.Reports;

public sealed record InvoiceLine(string BookingId, string ItemCode, int Quantity, int Days, long AmountCents);

public sealed record Invoice(
    string Customer,
    bool IsMember,
    ImmutableArray<InvoiceLine> Lines,
    long SubtotalCents,
    long DiscountCents,
    long TotalCents);

public static class Invoicing
{
    public const int MemberPercentOff = 10;

    public static readonly Func<long, long> NoDiscount = _ => 0;

    public static readonly Func<long, long> MemberDiscount = subtotal => subtotal * MemberPercentOff / 100;

    public static Func<long, long> DiscountFor(bool isMember) => isMember ? MemberDiscount : NoDiscount;

    public static Result<Invoice> For(
        IReadOnlyList<BookingState> bookings,
        string customer,
        Func<bool, Func<long, long>> discountFor) =>
        FindCustomer(bookings, customer)
            .ToResult(() => new Error(ErrorCode.UnknownCustomer, $"No bookings for customer '{customer.Trim()}'"))
            .Map(mine => Build(mine, discountFor));

    private static Option<ImmutableArray<BookingState>> FindCustomer(IReadOnlyList<BookingState> bookings, string customer)
    {
        var mine = bookings
            .Where(b => string.Equals(b.QuoteOf().Request.Customer, customer.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

        return mine.IsEmpty ? Option.None<ImmutableArray<BookingState>>() : Option.Some(mine);
    }

    private static Invoice Build(ImmutableArray<BookingState> mine, Func<bool, Func<long, long>> discountFor)
    {
        var first = mine[0].QuoteOf().Request;

        var lines = mine
            .Where(b => b.IsBillable())
            .Select(b => (Id: b.IdOf(), Quote: b.QuoteOf()))
            .Select(b => new InvoiceLine(b.Id, b.Quote.Request.ItemCode, b.Quote.Request.Quantity, b.Quote.Request.Days, b.Quote.TotalCents))
            .OrderByDescending(line => line.AmountCents)
            .ThenBy(line => line.BookingId, StringComparer.Ordinal)
            .ToImmutableArray();

        long subtotal = lines.Sum(line => line.AmountCents);
        long discount = discountFor(first.IsMember)(subtotal);

        return new Invoice(first.Customer, first.IsMember, lines, subtotal, discount, subtotal - discount);
    }
}
