using System;
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

        // Coordinated save (both files or neither):
        // 1) write both files next to the targets as *.tmp; a failure here touches no real file;
        // 2) keep a *.bak copy of the current bookings.csv;
        // 3) move bookings.tmp, then stock.tmp, over the real files;
        // 4) if the stock move fails, put the old bookings.csv back from the backup.
        // Bind alone would stop after a failure but would not undo a write that already happened.
        string bookingsTemp = bookingsPath + ".tmp";
        string stockTemp = stockPath + ".tmp";
        string bookingsBackup = bookingsPath + ".bak";
        bool hadBookings = File.Exists(bookingsPath);

        var written = DataFile.WriteLines("bookings", bookingsTemp, bookingLines)
            .Bind(_ => DataFile.WriteLines("stock", stockTemp, stockLines))
            .Bind(_ => hadBookings ? Copy(bookingsPath, bookingsBackup) : Result.Ok(bookingsBackup))
            .Bind(_ => Move(bookingsTemp, bookingsPath))
            .Bind(_ => Move(stockTemp, stockPath)
                .OnFail(() => Undo(bookingsPath, bookingsBackup, hadBookings)))
            .Map(_ => dir);

        DeleteQuietly(bookingsTemp);
        DeleteQuietly(stockTemp);
        DeleteQuietly(bookingsBackup);

        return written;
    }

    private static Result<string> OnFail(this Result<string> result, Action undo)
    {
        if (result is Result<string>.Fail)
            undo();
        return result;
    }

    private static void Undo(string bookingsPath, string backup, bool hadBookings)
    {
        try
        {
            if (hadBookings)
                File.Copy(backup, bookingsPath, overwrite: true);
            else
                File.Delete(bookingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the backup file stays on disk only if even this fails
        }
    }

    private static Result<string> Copy(string from, string to) =>
        FileStep(() => File.Copy(from, to, overwrite: true), to);

    private static Result<string> Move(string temp, string target) =>
        FileStep(() => File.Move(temp, target, overwrite: true), target);

    private static Result<string> FileStep(Action step, string target)
    {
        try
        {
            step();
            return Result.Ok(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail<string>(new Error(
                ErrorCode.FileUnwritable,
                $"Cannot write file '{target}': {ex.Message}"));
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort: a leftover .tmp file never replaces real data
        }
    }
}
