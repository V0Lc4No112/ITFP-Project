using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.States;

namespace Rental.Tests;

public class ReportTests
{
    private static Stock StockOf(params (string ItemCode, int Available)[] rows) =>
        Assert.IsType<Validation<Stock>.Valid>(Stock.Create(rows)).Value;

    private static readonly Stock SampleStock = StockOf(("BIKE-01", 3), ("SKI-02", 1), ("TENT-01", 2));

    private static BookingReport Book(Stock stock, params RentalRequest[] requests) =>
        Booking.ConfirmAll(
            RentalWorkflow.Run([.. requests.Select(SampleData.Validated)], PricingPolicies.Standard, RentalWorkflow.DefaultMaxDays),
            stock);

    private static readonly BookingReport SampleBooking = Book(SampleStock, [.. SampleData.All]);

    [Fact]
    public void ConfirmedQuotes_SkipsRejectedLines_KeepsBookingOrder()
    {
        var ids = SampleBooking.ConfirmedQuotes().Select(quote => quote.Request.Id);

        Assert.Equal(new[] { "R3", "R1", "R2", "R6" }, ids);
        Assert.Equal(5, SampleBooking.Lines.Length);
    }
}
