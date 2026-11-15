using System.Collections.Generic;
using System.IO;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.App.Io;

public static class SaveFolder
{
    public static Result<string> SaveStock(string dir, Stock stock)
    {
        string path = Path.Combine(dir, "stock.csv");

        return DataFile.WriteLines(
                "stock",
                path,
                StockParser.Lines(stock))
            .Map(_ => dir);
    }

    public static Result<string> SaveAll(
        string dir,
        IReadOnlyList<BookingState> bookings,
        Stock stock)
    {
        var bookingLines = BookingFile.Lines(bookings);
        var stockLines = StockParser.Lines(stock);

        string bookingsPath = Path.Combine(dir, "bookings.csv");
        string stockPath = Path.Combine(dir, "stock.csv");

        return DataFile.WriteLines(
                "bookings",
                bookingsPath,
                bookingLines)
            .Bind(_ => DataFile.WriteLines(
                "stock",
                stockPath,
                stockLines))
            .Map(_ => dir);
    }
}
