using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rental.Domain.Results;

namespace Rental.App.Io;

public static class DataFile
{
    // Lazy execution: reader opens only when enumeration starts.
    public static IEnumerable<string> StreamLines(
        Func<TextReader> open)
    {
        using var reader = open();

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    public static Result<IReadOnlyList<string>> ReadLines(
        string role,
        string path) =>
        ReadLines(role, path, () => new StreamReader(path));

    public static Result<IReadOnlyList<string>> ReadLines(
        string role,
        string path,
        Func<TextReader> open)
    {
        try
        {
            IReadOnlyList<string> lines =
                StreamLines(open).ToList();

            return Result.Ok(lines);
        }
        catch (FileNotFoundException)
        {
            return Result.Fail<IReadOnlyList<string>>(
                new Error(
                    ErrorCode.FileMissing,
                    $"Cannot load {role} file '{path}': file not found"));
        }
        catch (DirectoryNotFoundException)
        {
            return Result.Fail<IReadOnlyList<string>>(
                new Error(
                    ErrorCode.FileMissing,
                    $"Cannot load {role} file '{path}': folder not found"));
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail<IReadOnlyList<string>>(
                new Error(
                    ErrorCode.FileUnreadable,
                    $"Cannot load {role} file '{path}': {ex.Message}"));
        }
    }

    public static Result<int> WriteLines(
        string role,
        string path,
        IReadOnlyList<string> lines) =>
        WriteLines(role, path, lines, () => new StreamWriter(path));

    public static Result<int> WriteLines(
        string role,
        string path,
        IReadOnlyList<string> lines,
        Func<TextWriter> open)
    {
        try
        {
            using var writer = open();

            foreach (var line in lines)
            {
                writer.WriteLine(line);
            }

            return Result.Ok(lines.Count);
        }
        catch (DirectoryNotFoundException)
        {
            return Result.Fail<int>(
                new Error(
                    ErrorCode.FileUnwritable,
                    $"Cannot write {role} file '{path}': folder not found"));
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail<int>(
                new Error(
                    ErrorCode.FileUnwritable,
                    $"Cannot write {role} file '{path}': {ex.Message}"));
        }
    }
}
