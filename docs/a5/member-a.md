# Assignment 5: Member A, Input Parsing and Boundary Validation

**Area for the whole course:** Boundary & I/O (A5 parsing → A7 file loading → A8 native interop).
**Read first:** [../contracts.md](../contracts.md), especially the validation rules and the input header.

## What you build

File: `src/Rental.App/Io/RequestParser.cs` (the signatures already exist with an empty body).

```csharp
public sealed record ParseOutcome(ImmutableArray<RentalRequest> Valid, ImmutableArray<string> Errors);

public static class RequestParser
{
    public const string Header = "Id,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember";
    public static ParseOutcome Parse(IReadOnlyList<string>? lines);
}
```

Rules your parser must follow:
1. `lines == null` is **missing input**. Return one error, `"Input is missing"`, and no valid rows. Do not treat it as empty.
2. A header row with no data rows is a **valid empty dataset**: no rows and no errors.
3. A first line that isn't the expected header is an error.
4. Parse each field with `int.TryParse`, `long.TryParse`, `bool.TryParse` and `Enum.TryParse<Category>(..., ignoreCase: true, ...)`. Also check that the enum value is defined, because `Enum.TryParse` accepts numbers like `"7"`.
5. Check the rules from the contract: `Id`, `Customer` and `ItemCode` are not blank, `DailyRateCents > 0`, `Quantity >= 1`, `Days >= 1`, and the row has exactly 8 columns.
6. Every bad field produces an error naming the row and the field, for example `"Row 3: Days must be >= 1 (was -2)"`. One row can produce several errors.
7. `false` for `IsMember` is legitimate data, not "missing".
8. Never use `!`, and never default an invalid value to 0. A row with any error does **not** go into `Valid`.

Agree with D on this rule: if `Errors` is not empty, `Program` prints the errors and does not run the pipeline.

## Your tests

File: `tests/Rental.Tests/ParserTests.cs`

| Test | Asserts |
|---|---|
| `Parse_Null_ReturnsMissingInputError` | One error containing "missing"; `Valid` is empty |
| `Parse_HeaderOnly_ReturnsValidEmptyDataset` | `Valid` is empty **and** `Errors` is empty |
| `Parse_InvalidFields_RejectsEachWithMessage` | Negative days, non-numeric quantity, empty customer and unknown category each give an error naming the field; none are in `Valid` |
| `Parse_IsMemberFalse_IsAcceptedAsValue` | A row with `false` parses and has `IsMember == false` |

## Your README section (send to D)

- The input fields and their types.
- The validation rules, one line each.
- The difference between missing input, an empty dataset and invalid fields, with the demo command for each.

## Git

```
git switch main && git pull
git switch -c a5/validation-member-a
# implement + tests
dotnet test
git add src/Rental.App/Io tests/Rental.Tests/ParserTests.cs
git commit -m "[A5] Implement CSV parsing and boundary validation"
git push -u origin a5/validation-member-a
```
Then open a PR into `main` and ask another member to review it.

## Defense prep

- Explain why `null` input and a header-only file give different results.
- Show where a bad value is rejected and why it is never replaced with a default.
- **Practice live change:** add a rule such as "Quantity must be <= 10", add an assertion for it, and run the tests.
