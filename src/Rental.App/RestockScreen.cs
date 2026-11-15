
using System.Collections.Immutable;
using Rental.App.Workflows;

namespace Rental.App;

public static class RestockScreen
{
    public static ImmutableArray<string> Render(
        int deliveryCount,
        RestockOutcome outcome)
    {
        var lines = ImmutableArray.CreateBuilder<string>();

        int rejectedCount = outcome.Errors.Count;
        int acceptedCount = deliveryCount - rejectedCount;

        lines.Add("Restock summary:");
        lines.Add($"Accepted deliveries: {acceptedCount}");
        lines.Add($"Rejected deliveries: {rejectedCount}");

        if (rejectedCount > 0)
        {
            lines.Add("");
            lines.Add("Rejected deliveries:");

            foreach (var error in outcome.Errors)
            {
                lines.Add($"[{error.Code}] {error.Message}");
            }
        }

        if (outcome.SavedTo is not null)
        {
            lines.Add("");
            lines.Add($"Updated stock saved to {outcome.SavedTo}");
        }

        return lines.ToImmutable();
    }
}
