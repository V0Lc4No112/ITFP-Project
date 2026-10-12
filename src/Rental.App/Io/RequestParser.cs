using System.Collections.Immutable;
using Rental.Domain.Models;

namespace Rental.App.Io;

public sealed record ParseOutcome(ImmutableArray<RentalRequest> Valid, ImmutableArray<string> Errors);

public static class RequestParser
{
    public const string Header = "Id,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember";

    /// <summary>
    /// Member A: null means missing input (an error); header only means a valid
    /// empty dataset. Every invalid field produces an error naming the row and field.
    /// </summary>
    public static ParseOutcome Parse(IReadOnlyList<string>? lines) =>
        throw new NotImplementedException();
}
