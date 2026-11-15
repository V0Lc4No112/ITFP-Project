using Rental.App.Io;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Results;

namespace Rental.App.Workflows;

public sealed record RestockOutcome(
    IReadOnlyList<Error> Errors,
    string? SavedTo);

public static class RestockWorkflow
{
    public static Result<RestockOutcome> Execute(
        string stockPath,
        string deliveriesPath,
        string? saveDirectory = null)
    {
        return StockParser.Load(stockPath)
            .Bind(stock => DeliveryParser.Load(deliveriesPath)
                .Map(deliveries => Restocking.Apply(stock, deliveries)))
            .Bind(result =>
            {
                if (saveDirectory is null)
                {
                    return Result.Ok(new RestockOutcome(
                        result.Errors,
                        null));
                }

                return SaveFolder.SaveStock(saveDirectory, result.Stock)
                    .Map(savedTo => new RestockOutcome(
                        result.Errors,
                        savedTo));
            });
    }
}
