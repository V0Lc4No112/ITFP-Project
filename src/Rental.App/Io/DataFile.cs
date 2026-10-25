
using System;
using System.Collections.Generic;
using System.IO;
using Rental.Domain.Results;

namespace Rental.App.Io;

public static class DataFile
{
    public static Result<IReadOnlyList<string>> ReadLines(
        string role,
        string path)
    {
        try
        {
            IReadOnlyList<string> lines = File.ReadAllLines(path);
            return Result.Ok(lines);
        }
        catch (FileNotFoundException)
        {
            return Result.Fail<IReadOnlyList<string>>(
                new Error(
                    ErrorCode.FileMissing,
                    $"Cannot load {role} file '{path}': file not found"
                )
            );
        }
        catch (DirectoryNotFoundException)
        {
            return Result.Fail<IReadOnlyList<string>>(
                new Error(
                    ErrorCode.FileMissing,
                    $"Cannot load {role} file '{path}': folder not found"
                )
            );
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail<IReadOnlyList<string>>(
                new Error(
                    ErrorCode.FileUnreadable,
                    $"Cannot load {role} file '{path}': {ex.Message}"
                )
            );
        }
    }
}
