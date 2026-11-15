using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Domain.Pipeline;

public static class Restocking
{
    public static Result<Stock> ApplyOne(Stock stock, Delivery delivery)
    {
        if (string.IsNullOrWhiteSpace(delivery.ItemCode))
            return Result.Fail<Stock>(new Error(
                ErrorCode.InvalidEvent,
                "Delivery ItemCode must not be blank"));

        if (delivery.Added < 1)
            return Result.Fail<Stock>(new Error(
                ErrorCode.InvalidEvent,
                $"Added quantity must be >= 1 (was {delivery.Added})"));

        return stock.Available(delivery.ItemCode).ToResult(() =>
            new Error(
                ErrorCode.UnknownItem,
                $"Unknown item: {delivery.ItemCode}"))
            .Map(_ => stock.Release(delivery.ItemCode, delivery.Added));
    }

    public static (Stock Stock, IReadOnlyList<Error> Errors) Apply(
        Stock stock,
        IReadOnlyList<Delivery> deliveries)
    {
        var currentStock = stock;
        var errors = new List<Error>();

        foreach (Delivery delivery in deliveries)
        {
            switch (ApplyOne(currentStock, delivery))
            {
                case Result<Stock>.Ok ok:
                    currentStock = ok.Value;
                    break;

                case Result<Stock>.Fail fail:
                    errors.Add(fail.Error);
                    break;
            }
        }

        return (currentStock, errors);
    }
}
