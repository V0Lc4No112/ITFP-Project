using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.Reports;
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

    [Fact]
    public void RevenueByCategory_SampleBooking_GroupsAndTotals()
    {
        var rows = RentalReports.RevenueByCategory(SampleBooking);

        Assert.Equal(
            new[]
            {
                new CategoryRevenue(Category.Camping, 1, 1, 35000),
                new CategoryRevenue(Category.Bike, 2, 3, 27000),
                new CategoryRevenue(Category.Ski, 1, 1, 12000),
            },
            rows);
    }

    [Fact]
    public void RevenueByCategory_TiedRevenue_OrdersByCategoryName()
    {
        var booking = Book(
            SampleStock,
            new RentalRequest("T1", "Tia", "SKI-02", Category.Ski, 6000, 1, 2, false),
            new RentalRequest("T2", "Tom", "BIKE-01", Category.Bike, 4000, 1, 3, false));

        var rows = RentalReports.RevenueByCategory(booking);

        Assert.Equal(
            new[]
            {
                new CategoryRevenue(Category.Bike, 1, 1, 12000),
                new CategoryRevenue(Category.Ski, 1, 1, 12000),
            },
            rows);
    }

    [Fact]
    public void RevenueByCategory_EmptyBooking_IsEmpty() =>
        Assert.Empty(RentalReports.RevenueByCategory(Book(SampleStock)));

    [Fact]
    public void RevenueByCategory_NoConfirmedBooking_IsEmpty()
    {
        var booking = Book(StockOf(("BOAT-01", 1)), [.. SampleData.All]);

        Assert.Equal(5, booking.Lines.Length);
        Assert.Empty(RentalReports.RevenueByCategory(booking));
    }

    [Fact]
    public void LowStock_AfterSampleBooking_TiesOrderedByItemCode()
    {
        var rows = RentalReports.LowStock(SampleBooking.RemainingStock, 1);

        Assert.Equal(
            new[]
            {
                new LowStockLine("BIKE-01", 0),
                new LowStockLine("SKI-02", 0),
                new LowStockLine("TENT-01", 1),
            },
            rows);
    }

    [Fact]
    public void LowStock_NothingAtOrBelowThreshold_IsEmpty() =>
        Assert.Empty(RentalReports.LowStock(SampleStock, 0));

    [Fact]
    public void LowStock_EmptyStockOrNegativeThreshold_IsEmpty()
    {
        Assert.Empty(RentalReports.LowStock(StockOf(), 5));
        Assert.Empty(RentalReports.LowStock(StockOf(("SKI-02", 0)), -1));
    }
}
