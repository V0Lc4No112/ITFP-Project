using Rental.App;
using Rental.App.Workflows;
using Rental.Domain.Results;

namespace Rental.Tests;

public class RestockScreenTests
{
    [Fact]
    public void Render_ShowsAcceptedAndRejectedDeliveries()
    {
        var outcome = new RestockOutcome(
            [
                new Error(ErrorCode.InvalidEvent, "Added quantity must be >= 1"),
                new Error(ErrorCode.UnknownItem, "Unknown item: KAYAK-01")
            ],
            null);

        var lines = RestockScreen.Render(4, outcome);

        Assert.Contains("Accepted deliveries: 2", lines);
        Assert.Contains("Rejected deliveries: 2", lines);
        Assert.Contains("Rejected deliveries:", lines);
    }

    [Fact]
    public void Render_ShowsSaveLocation_WhenStockWasSaved()
    {
        var outcome = new RestockOutcome([], "output");

        var lines = RestockScreen.Render(2, outcome);

        Assert.Contains("Accepted deliveries: 2", lines);
        Assert.Contains("Rejected deliveries: 0", lines);
        Assert.Contains("Updated stock saved to output", lines);
    }
}
