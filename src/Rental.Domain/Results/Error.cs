using System.Collections.Immutable;

namespace Rental.Domain.Results;

public enum ErrorCode
{
    InvalidRequest,
    UnknownPolicy,
    NoStockRecord,
    NotEnoughStock,
    FileMissing,
    FileUnreadable,
    MalformedFile,
}

public sealed record Error
{
    public ErrorCode Code { get; }

    public string Message { get; }

    public ImmutableArray<string> Details { get; }

    public Error(ErrorCode code, string message, ImmutableArray<string> details = default)
    {
        Code = code;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Error needs a message", nameof(message))
            : message;
        Details = details.IsDefault ? [] : details;
    }

    public bool IsOperational =>
        Code is ErrorCode.FileMissing or ErrorCode.FileUnreadable or ErrorCode.MalformedFile;
}
