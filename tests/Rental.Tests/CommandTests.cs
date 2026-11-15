using Rental.App;
using Rental.Domain.Results;

namespace Rental.Tests;

/// <summary>Member D: the pure command-line parser. No file is read here.</summary>
public class CommandTests
{
    private static Command Parsed(params string[] args) =>
        Assert.IsType<Result<Command>.Ok>(Commands.Parse(args)).Value;

    private static Error Failed(params string[] args) =>
        Assert.IsType<Result<Command>.Fail>(Commands.Parse(args)).Error;

    [Fact]
    public void Parse_EachCommand_GivesItsCase()
    {
        Assert.Equal(new Command.Quote("r.csv", "weekly"), Parsed("quote", "r.csv", "weekly"));
        Assert.IsType<Command.Book>(Parsed("book", "r.csv", "standard", "s.csv"));
        Assert.Equal(
            new Command.Restock("s.csv", "d.csv", Option.None<string>()),
            Parsed("restock", "s.csv", "d.csv"));
        Assert.Equal(
            new Command.Cancel("b.csv", "c.csv", "s.csv", Option.None<string>()),
            Parsed("CANCEL", "b.csv", "c.csv", "s.csv"));
    }

    [Fact]
    public void Parse_OldThreeAndFourArgumentForms_AreBook()
    {
        Assert.Equal(
            new Command.Book("r.csv", "standard", "s.csv", Option.None<string>(), Option.None<string>()),
            Parsed("r.csv", "standard", "s.csv"));
        Assert.Equal(
            new Command.Book("r.csv", "standard", "s.csv", Option.Some("out.csv"), Option.None<string>()),
            Parsed("r.csv", "standard", "s.csv", "out.csv"));
    }

    [Fact]
    public void Parse_SaveOption_IsSome_AndOnlyAllowedLast()
    {
        var book = Assert.IsType<Command.Book>(Parsed("book", "r.csv", "standard", "s.csv", "rep.csv", "--save", "out"));
        Assert.Equal(Option.Some("rep.csv"), book.ReportPath);
        Assert.Equal(Option.Some("out"), book.SaveDir);

        Assert.Equal(ErrorCode.InvalidRequest, Failed("book", "r.csv", "standard", "--save", "out", "s.csv").Code);
        Assert.Equal(ErrorCode.InvalidRequest, Failed("cancel", "b.csv", "c.csv", "s.csv", "--save").Code);
        // quote saves nothing, so --save is a usage error there
        Assert.Equal(ErrorCode.InvalidRequest, Failed("quote", "r.csv", "standard", "--save", "out").Code);
    }

    [Fact]
    public void Parse_UnknownOrMissingCommand_FailsWithUsageLines()
    {
        var unknown = Failed("refund", "a", "b", "c", "d");
        Assert.Equal(ErrorCode.InvalidRequest, unknown.Code);
        Assert.Contains("Unknown command 'refund'", unknown.Message);
        Assert.Equal(Commands.Usage, unknown.Details);

        Assert.Contains("No command given", Failed().Message);
    }

    [Fact]
    public void Parse_KnownCommandWithWrongArgumentCount_FailsInsteadOfGuessing()
    {
        Assert.Contains("Wrong arguments for 'cancel'", Failed("cancel", "b.csv", "c.csv").Message);
        Assert.Contains("Wrong arguments for 'quote'", Failed("quote", "r.csv").Message);
    }
}
