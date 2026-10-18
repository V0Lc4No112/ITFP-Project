# Equipment Rental Shop

Introduction to Functional Programming, 2026/2027. A group project built up across Assignments 5 to 8 and the final project.

The shop rents bikes, skis and camping gear. The program reads rental requests from a CSV file, keeps the eligible ones, prices them with a selectable pricing policy, and prints the quotes. Since Assignment 6 it also confirms each quote against a stock file and prints which bookings were accepted and what stock is left.

## Intended users

Counter staff at a small rental shop who need a quick, repeatable quote for a batch of requests, and the team members who extend the pipeline in later assignments.

## Team

| Member | Name / group | Area | A5 responsibility |
|---|---|---|---|
| A | Alimzhan Kabylkhanov, IT-2511 | Boundary & I/O | CSV parsing and validation |
| B | Daniyar Azizayev, IT-2511 | Functional core | Stage 1 and Stage 3 |
| C | Mukhamejan Abdikerim, IT-2511 | Rules & states | Stage 2 (higher-order function) and pricing policies |
| D | Mereikhan Sholanbayev, IT-2511 | Integration & docs | Composition, `Program.cs`, dataset, README |

## Requirements

- .NET SDK 10 (pinned in `global.json`, rolls forward to the latest 10.0.x feature band)

## Build, run, test

Run these from the repository root:

```
dotnet build
dotnet test
dotnet run --project src/Rental.App -- data/requests.csv standard data/stock.csv
dotnet run --project src/Rental.App -- data/requests.csv discount data/stock.csv
dotnet run --project src/Rental.App -- data/requests.csv standard data/stock-missing-item.csv
dotnet run --project src/Rental.App -- data/requests-invalid.csv standard data/stock.csv
dotnet test --filter Confirm_Twice
```

Usage: `dotnet run --project src/Rental.App -- <requests.csv> <standard|discount> <stock.csv>`. Since A6 the stock file is required. Wrong arguments print the usage; a missing file, invalid rows, an unknown policy or an invalid stock file print a clear error and exit with code 1. The A6 demo table is in [Assignment 6: safe booking](#a6-demo-commands).

### Demo commands

| Case | Command | Expected |
|---|---|---|
| Normal, Standard | `dotnet run --project src/Rental.App -- data/requests.csv standard data/stock.csv` | R3, R1, R2, R6, R5; grand total 790.00; count 5 |
| Normal, Discount | `dotnet run --project src/Rental.App -- data/requests.csv discount data/stock.csv` | R3, R1, R2, R6, R5; grand total 697.00; count 5 |
| Invalid input | `dotnet run --project src/Rental.App -- data/requests-invalid.csv standard data/stock.csv` | Every row error is printed, the pipeline does not run, exit code 1 |
| Empty dataset | `dotnet run --project src/Rental.App -- data/requests-empty.csv standard data/stock.csv` | "No eligible rentals."; grand total 0.00; count 0 |
| Unknown policy | `dotnet run --project src/Rental.App -- data/requests.csv vip data/stock.csv` | `Unknown policy`, exit code 1 |

## Input

`data/requests.csv`, header `Id,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember`.

| Field | Type | Meaning |
|---|---|---|
| `Id` | string | Request id, for example `R1` |
| `Customer` | string | Customer name (trimmed in Stage 1) |
| `ItemCode` | string | Item rented, for example `BIKE-01` |
| `Category` | `Bike`, `Ski` or `Camping` | Kind of equipment |
| `DailyRateCents` | long | Price per item per day, in cents |
| `Quantity` | int | Number of items |
| `Days` | int | Rental length in days |
| `IsMember` | bool | Shop member flag (`true` / `false`) |

### Validation (Member A)

`RequestParser.Parse(IReadOnlyList<string>? lines)` ([src/Rental.App/Io/RequestParser.cs](src/Rental.App/Io/RequestParser.cs)) returns `ParseOutcome(Valid, Errors)`. It never throws and never fixes a value: each row is either fully valid or rejected with a message for every bad field. Rows are numbered with the header as row 1, so the first data row is row 2.

| Input | Result |
|---|---|
| `null` (missing) | Error `Input is missing`, no rows |
| No lines at all | Error `Row 1: Header is missing` |
| First line is not `RequestParser.Header` | Error `Row 1: Invalid header`, no rows parsed |
| Header only (empty) | No rows and no errors: a **valid empty dataset** |
| Rows with bad fields (invalid) | Each bad field reported, that row skipped; the other rows are still checked |

Checks per row (fields are trimmed first):

- Exactly 8 columns.
- `Id`, `Customer` and `ItemCode` are not blank.
- `Category` is `Bike`, `Ski` or `Camping` (case-insensitive).
- `DailyRateCents` is a number and `> 0`.
- `Quantity` and `Days` are integers and `>= 1`.
- `IsMember` is `true` or `false`.

Example, `data/requests-invalid.csv` prints:

```
Input has 4 error(s); the pipeline was not run:
  Row 2: Days must be >= 1 (was -2)
  Row 3: Quantity must be an integer (was two)
  Row 4: Customer must not be blank
  Row 5: Category is invalid (was Boat)
```

## Pipeline

`Parse` (boundary) → `SelectEligible` → `PriceAll` → `BuildSummary` → print (boundary).

The composition is in `RentalWorkflow.Run` ([src/Rental.Domain/Pipeline/RentalWorkflow.cs](src/Rental.Domain/Pipeline/RentalWorkflow.cs)): each stage's output is the next stage's input, in one visible place. `Program` reads and parses the file, picks the policy from the command line, calls `Run`, and prints only after `Run` returns, so the core never touches the console or the file system and can be tested with plain records.

### Stage 1: `SelectEligible` (Member B)

[src/Rental.Domain/Pipeline/Stages.cs](src/Rental.Domain/Pipeline/Stages.cs)

| | |
|---|---|
| Input | `IReadOnlyList<RentalRequest> requests`, `int maxDays` |
| Output | `ImmutableArray<RentalRequest>` |
| Rule | Keep requests with `Days <= maxDays`; trim `Customer` with `with`, which makes a new record, so the caller's list is never changed |

Worked example with `maxDays = 14`: R3 (14 days) is kept and R4 (15 days) is dropped, so R1, R2, R3, R5, R6 go on to Stage 2.

### Stage 2: `PriceAll` and the pricing policies (Member C)

[src/Rental.Domain/Pipeline/Pricing.cs](src/Rental.Domain/Pipeline/Pricing.cs), [src/Rental.Domain/Policies/PricingPolicies.cs](src/Rental.Domain/Policies/PricingPolicies.cs)

| | |
|---|---|
| Input | `ImmutableArray<RentalRequest> eligible`, `Func<RentalRequest, long> policy` |
| Output | `ImmutableArray<PricedRental>`, in the same order as the input |
| Rule | Call `policy` exactly once per request and pair the request with its total |

`PriceAll` is the **higher-order function**: it takes the pricing rule as a parameter and does not know which policy it was given. The two policies are `static readonly Func<RentalRequest, long>` values:

- `Standard`: `DailyRateCents × Quantity × Days`.
- `LongRentalDiscount`: Standard, then `base * 85 / 100` when `Days >= 5` (integer division rounds down to whole cents).

`PricingPolicies.FromName` maps `standard` and `discount` (case-insensitive) to these functions and returns `null` for anything else, which `Program` reports as `Unknown policy`.

Worked example: R1 (1500 × 2 × 5) is 15000 under Standard. It has 5 days, so the discount applies: 15000 × 85 / 100 = 12750. R2 has 3 days, so it is 12000 under both policies.

### Stage 3: `BuildSummary` (Member B)

[src/Rental.Domain/Pipeline/Stages.cs](src/Rental.Domain/Pipeline/Stages.cs)

| | |
|---|---|
| Input | `ImmutableArray<PricedRental> priced` |
| Output | `QuoteSummary(Quotes, GrandTotalCents, Count)` |
| Rule | Order by `TotalCents` descending, then `Id` ascending (ordinal); grand total is the sum of `TotalCents`; count is the number of quotes |

Worked example (Standard): R3 35000, R1 15000, R2 12000, R6 12000, R5 5000. R2 and R6 tie, so `Id` puts R2 first. Grand total 79000, count 5.

## Business rules

| Rule | Owner | Definition |
|---|---|---|
| Validation | A | Every field present; `DailyRateCents > 0`; `Quantity >= 1`; `Days >= 1`; `Category` is a known value. Invalid rows get a clear error and are never silently fixed |
| Missing vs empty | A | `null` input is an error (`"Input is missing"`). Header only is a valid empty dataset |
| Errors stop the run | A + D | If `Errors` is not empty, `Program` prints them and does not run the pipeline |
| Eligibility (Stage 1) | B | `Days <= maxDays` (14). **14 is accepted, 15 is excluded.** `Customer` is trimmed |
| Standard policy | C | `DailyRateCents × Quantity × Days` |
| Long-rental discount | C | Standard, then 15% off when `Days >= 5`: `base * 85 / 100` (integer division, so it rounds down to whole cents). **5 days gets the discount** |
| Ordering (Stage 3) | B | `TotalCents` descending, then `Id` ascending |
| Policy selection | C + D | Chosen in `Program` from the command line (`standard` / `discount`) and passed as an argument. An unknown name is a clear error, not a fallback |

## Dataset and expected results

| Id | Customer | Item | Category | Rate | Qty | Days | Member | Standard | LongRentalDiscount |
|---|---|---|---|---|---|---|---|---|---|
| R1 | Anna | BIKE-01 | Bike | 1500 | 2 | 5 | true | 15000 | **12750** |
| R2 | Ben | SKI-02 | Ski | 4000 | 1 | 3 | false | 12000 | 12000 |
| R3 | Cara | TENT-01 | Camping | 2500 | 1 | 14 | false | 35000 | **29750** (boundary: accepted) |
| R4 | Dan | BIKE-01 | Bike | 1500 | 1 | 15 | true | excluded | excluded |
| R5 | Eve | TENT-01 | Camping | 2500 | 2 | 1 | false | 5000 | 5000 |
| R6 | Finn | BIKE-01 | Bike | 1500 | 1 | 8 | false | 12000 | **10200** |

- Standard: order R3, R1, R2, R6, R5 (R2 and R6 tie at 12000, so Id decides). Grand total **79000**, count 5.
- Discount: order R3, R1, R2, R6, R5. Grand total **69700**, count 5.
- `data/requests-invalid.csv`: a negative `Days` (X1), a non-numeric `Quantity` (X2), an empty `Customer` (X3), and an unknown `Category` (X4).
- `data/requests-empty.csv`: the header row only (a **valid empty dataset**).

## Assignment 6: safe booking

A6 adds a booking step after the A5 pipeline. Each quote is confirmed against a stock file, in summary order, and every accepted booking reserves its items. The types make the wrong order of steps impossible to write instead of checking for it at run time.

Pipeline: `Parse` (boundary) → `ValidatedRental.Create` → `RentalWorkflow.Run` → `Booking.ConfirmAll` → print (boundary). Stock is read from `stock.csv` and parsed with `StockParser.Parse`; the remaining stock is printed, never written back to the file.

### Before and after

**Before.** This is the unsafe design the A6 types replace. It lives in this README only and is not part of the project.

```csharp
// UNSAFE: one mutable object, and the caller must remember the order.
public class RentalOrder
{
    public RentalRequest Request;
    public bool IsValid;
    public long TotalCents;
    public bool IsConfirmed;
    public Dictionary<string, int>? Stock;

    public void Validate() { /* sets IsValid */ }
    public void Price(Func<RentalRequest, long> policy) { TotalCents = policy(Request); }
    public void LoadStock(Dictionary<string, int> stock) { Stock = stock; }
    public void Confirm()
    {
        Stock![Request.ItemCode] -= Request.Quantity;
        IsConfirmed = true;
    }
}
```

The required order is `Validate` → `Price` → `LoadStock` → `Confirm`, and `Confirm` only once. Nothing enforces it:

| Mistake | What happens |
|---|---|
| `Confirm` before `Validate` | X1 with `Days = -2` is confirmed |
| `Confirm` before `Price` | A booking is confirmed with a total of 0 |
| `Confirm` without `LoadStock` | `Stock!` is null, so it crashes with a `NullReferenceException` |
| `Confirm` twice | BIKE-01 goes 3 → 1 → -1 and the same order is booked twice |
| R5 after R3 | Two tents are taken when one is left, TENT-01 goes to -1, and R5 is still "confirmed" |

**After.** The safe call site. Each step returns a new value of a new type, and the next step only accepts that type:

```csharp
// RequestParser.Parse already ran ValidatedRental.Create on every row.
ImmutableArray<ValidatedRental> rentals = parsed.Valid;
QuoteSummary summary = RentalWorkflow.Run(rentals, policy, RentalWorkflow.DefaultMaxDays);
BookingReport report = Booking.ConfirmAll(summary, stock);
// inside ConfirmAll, for each quote:
ConfirmResult result = BookingTransitions.Confirm(new BookingState.Quoted(quote), current);
```

Calls that no longer compile:

| Call | Why it fails |
|---|---|
| `RentalWorkflow.Run(SampleData.All, policy, 14)` | `Run` takes `ValidatedRental`, not `RentalRequest` |
| `new ValidatedRental(request)` | The constructor is private; the only way in is `ValidatedRental.Create` |
| `new Stock(...)`, or `stock.Reserve("BIKE-01", 2)` from the app | The constructor is private and `Reserve` is `internal`, so only the domain can take stock |
| `validation.Value` on a `Validation<T>` | The base type has no `Value`; you must `switch` on `Valid` / `Invalid` |
| A new case added to a union from outside | The unions are closed (private constructor), so no other case can exist |

Confirming the same booking twice still compiles, but it is safe: see the repeat rule below.

### Cases of the three unions

All three are closed (private constructor, sealed cases), and every `switch` over them names each case and ends with a default that throws.

| Union | Case | Data | Meaning |
|---|---|---|---|
| `Validation<T>` | `Valid` | `T Value` | The input passed every rule |
| | `Invalid` | `ImmutableArray<string> Reasons` | Every broken rule, never empty |
| `BookingState` | `Quoted` | `PricedRental Quote` | Priced, not booked yet |
| | `Confirmed` | `PricedRental Quote`, `string BookingId` | Booked; the id is `B-<Id>`, for example `B-R3` |
| `ConfirmResult` | `Accepted` | `BookingState.Confirmed Booking`, `Stock Stock` | Booked, with the stock left after the reservation |
| | `Rejected` | `string Reason` | Not booked; the stock is unchanged |

### Factory rules

| Factory | Valid when | Otherwise |
|---|---|---|
| `ValidatedRental.Create(RentalRequest)` | `Id`, `Customer`, `ItemCode` not blank; `Category` defined; `DailyRateCents > 0`; `Quantity >= 1`; `Days >= 1` | `Invalid` with every reason collected, not just the first |
| `Stock.Create(rows)` | Each `ItemCode` not blank and listed once; `Available >= 0`. No rows at all is a valid empty stock | `Invalid` with every reason |
| `StockParser.Parse(lines)` | Header is `ItemCode,Available`, then each row parses and `Stock.Create` accepts the rows | `Invalid`; `Program` prints the reasons and exits with code 1 |

### Transitions

`BookingTransitions.Confirm(BookingState state, Stock stock)`. All checks happen before `Reserve`, and neither input is ever changed.

| From | Condition | Result |
|---|---|---|
| `Quoted` | No stock record for the item | `Rejected("<Id>: no stock record for <code>")` |
| `Quoted` | `Quantity` is more than available | `Rejected("<Id>: not enough <code> (requested q, available n)")` |
| `Quoted` | `Quantity` is at most available (equal is accepted and leaves 0) | `Accepted(Confirmed(quote, "B-<Id>"), stock with Quantity reserved)` |
| `Confirmed` | Any | `Accepted(same booking, same stock)` |

**Repeat rule.** Confirming a booking that is already `Confirmed` returns the same booking and the same stock, so stock is reserved only once. Show it with:

```
dotnet test --filter Confirm_Twice
```

### Booking all quotes (Member D)

[src/Rental.Domain/Pipeline/Booking.cs](src/Rental.Domain/Pipeline/Booking.cs)

| | |
|---|---|
| Input | `QuoteSummary summary`, `Stock stock` |
| Output | `BookingReport(Lines, RemainingStock, ConfirmedTotalCents)`; each `BookingLine` is a quote and its `ConfirmResult` |
| Rule | Go through the quotes in summary order. `Accepted` passes its new stock to the next quote and adds the quote total; `Rejected` changes nothing. The stock passed in is never changed. No console, no files |

Worked example, `data/stock.csv` (BIKE-01 3, SKI-02 1, TENT-01 2) with Standard: R3 takes 1 tent (2 → 1), R1 takes 2 bikes (3 → 1), R2 takes the only ski (1 → 0), R6 takes the last bike (1 → 0), and R5 needs 2 tents but only 1 is left, so it is rejected. Confirmed total 350 + 150 + 120 + 120 = 740.00. Stock left BIKE-01=0, SKI-02=0, TENT-01=1.

### Stock file

`data/stock.csv`, header `ItemCode,Available`:

| ItemCode | Available |
|---|---|
| BIKE-01 | 3 |
| SKI-02 | 1 |
| TENT-01 | 2 |

`data/stock-missing-item.csv` is the same file without the SKI-02 row.

### A6 demo commands

| Case | Command | Expected |
|---|---|---|
| Standard | `dotnet run --project src/Rental.App -- data/requests.csv standard data/stock.csv` | B-R3, B-R1, B-R2, B-R6 confirmed; R5 rejected `not enough TENT-01 (requested 2, available 1)`; confirmed total 740.00; stock left BIKE-01=0, SKI-02=0, TENT-01=1 |
| Discount | `dotnet run --project src/Rental.App -- data/requests.csv discount data/stock.csv` | Same bookings; confirmed total 647.00; same stock left |
| Missing stock record | `dotnet run --project src/Rental.App -- data/requests.csv standard data/stock-missing-item.csv` | B-R3, B-R1, B-R6 confirmed; R2 rejected `no stock record for SKI-02`; R5 rejected; confirmed total 620.00; stock left BIKE-01=0, TENT-01=1 |
| Invalid requests | `dotnet run --project src/Rental.App -- data/requests-invalid.csv standard data/stock.csv` | The same 4 error lines as A5, nothing confirmed, exit code 1 |

The A5 quote list and grand totals (790.00 / 697.00) do not change. Output of the first command:

```
Policy: standard
R3   Cara           350.00
R1   Anna           150.00
R2   Ben            120.00
R6   Finn           120.00
R5   Eve             50.00
Grand total: 790.00
Count: 5

Bookings:
B-R3   confirmed      350.00
B-R1   confirmed      150.00
B-R2   confirmed      120.00
B-R6   confirmed      120.00
R5     REJECTED   R5: not enough TENT-01 (requested 2, available 1)
Confirmed total: 740.00
Stock left: BIKE-01=0, SKI-02=0, TENT-01=1
```

### A6 requirement map

| Assignment task | File | Method / type |
|---|---|---|
| Closed union for results | `src/Rental.Domain/States/` | `Validation<T>` |
| Closed unions for states | `src/Rental.Domain/States/` | `BookingState`, `ConfirmResult` |
| Smart constructor (request) | `src/Rental.Domain/States/ValidatedRental.cs` | `ValidatedRental.Create` |
| Smart constructor (stock) | `src/Rental.Domain/States/` | `Stock.Create` |
| Parse stock at the boundary | `src/Rental.App/Io/StockParser.cs` | `StockParser.Parse` |
| State transition | `src/Rental.Domain/States/BookingTransitions.cs` | `BookingTransitions.Confirm` |
| Pipeline takes only validated input | `src/Rental.Domain/Pipeline/RentalWorkflow.cs` | `RentalWorkflow.Run` |
| Confirm every quote, pass the stock on | `src/Rental.Domain/Pipeline/Booking.cs` | `Booking.ConfirmAll` |
| Exhaustive `switch` at the boundary | `src/Rental.App/Program.cs` | top-level statements |
| Stock sample data | `data/` | `stock.csv`, `stock-missing-item.csv` |
| Validated test records | `tests/Rental.Tests/SampleData.cs` | `SampleData.Validated`, `SampleData.ValidatedAll` |

### A6 tests

| Test | File | Expected |
|---|---|---|
| `ConfirmAll_SampleData_ConfirmsFourRejectsR5` | `BookingTests.cs` | B-R3, B-R1, B-R2, B-R6 accepted; R5 rejected with its reason; total 74000; stock 0 / 0 / 1 |
| `ConfirmAll_MissingStockRecord_RejectsOnlyThatItem` | `BookingTests.cs` | Without SKI-02: R2 and R5 rejected; total 62000 |
| `ConfirmAll_DoesNotChangeInputStock` | `BookingTests.cs` | The stock passed in still reports 3 / 1 / 2 |
| `NormalWorkflow_Standard_MatchesDocumentedResult`, `NormalWorkflow_Discount_MatchesDocumentedResult`, `EmptyDataset_ReturnsEmptySummary` | `WorkflowTests.cs` | The A5 results, now from `SampleData.ValidatedAll` |
| _TODO (A)_ | _TODO (A)_ | _TODO (A)_ |
| _TODO (B)_ | _TODO (B)_ | _TODO (B)_ |
| _TODO (C), including `Confirm_Twice`_ | _TODO (C)_ | _TODO (C)_ |

### A6 members

| Member | Logic | Tests | Docs |
|---|---|---|---|
| A | `ValidatedRental`, `StockParser`; the parser keeps text checks only | _TODO (A)_ | _TODO (A): validation and stock file section_ |
| B | `Validation<T>`, `BookingState`, `ConfirmResult`, `Stock` | _TODO (B)_ | _TODO (B): unions and stock section_ |
| C | `BookingTransitions.Confirm` | _TODO (C)_ | _TODO (C): transitions and repeat rule section_ |
| D | `RentalWorkflow.Run` on validated input, `Booking.ConfirmAll`, `Program.cs`, `data/stock*.csv` | `BookingTests.cs`, `WorkflowTests.cs`, `SampleData.cs` | This section, before and after, demo table |

## Requirement map

| Assignment task | File | Method |
|---|---|---|
| Read input at the boundary | `src/Rental.App/Program.cs` | top-level statements (`File.ReadAllLines`) |
| Parse and validate | `src/Rental.App/Io/RequestParser.cs` | `RequestParser.Parse` |
| Stage 1: filter | `src/Rental.Domain/Pipeline/Stages.cs` | `Stages.SelectEligible` |
| Stage 2: higher-order function | `src/Rental.Domain/Pipeline/Pricing.cs` | `Pricing.PriceAll` |
| Two policies passed as functions | `src/Rental.Domain/Policies/PricingPolicies.cs` | `Standard`, `LongRentalDiscount`, `FromName` |
| Stage 3: aggregate | `src/Rental.Domain/Pipeline/Stages.cs` | `Stages.BuildSummary` |
| Compose the pipeline | `src/Rental.Domain/Pipeline/RentalWorkflow.cs` | `RentalWorkflow.Run` |
| Print after the core returns | `src/Rental.App/Program.cs` | top-level statements |
| Sample data | `data/` | `requests.csv`, `requests-invalid.csv`, `requests-empty.csv` |
| Shared test records | `tests/Rental.Tests/SampleData.cs` | `SampleData.R1` to `R6`, `SampleData.All` |

## Tests

| Test | File | Expected | Latest `dotnet test` |
|---|---|---|---|
| `NormalWorkflow_Standard_MatchesDocumentedResult` | `WorkflowTests.cs` | Order R3, R1, R2, R6, R5; total 79000; count 5 | Passed (2026-10-13) |
| `NormalWorkflow_Discount_MatchesDocumentedResult` | `WorkflowTests.cs` | Order R3, R1, R2, R6, R5; total 69700; count 5 | Passed (2026-10-13) |
| `EmptyDataset_ReturnsEmptySummary` | `WorkflowTests.cs` | Count 0, total 0, no exception | Passed (2026-10-13) |
| `Parse_Null_ReturnsMissingInputError` | `ParserTests.cs` | One "missing" error, no valid rows | Passed (2026-10-13) |
| `Parse_HeaderOnly_ReturnsValidEmptyDataset` | `ParserTests.cs` | No rows and no errors | Passed (2026-10-13) |
| `Parse_InvalidFields_RejectsEachWithMessage` | `ParserTests.cs` | Each bad field named; none valid | Passed (2026-10-13) |
| `Parse_IsMemberFalse_IsAcceptedAsValue` | `ParserTests.cs` | `IsMember == false` | Passed (2026-10-13) |
| `SelectEligible_ExactlyMaxDays_IsIncluded_OneMore_IsExcluded` | `StageTests.cs` | 14 kept, 15 dropped | Passed (2026-10-13) |
| `SelectEligible_DoesNotModifyCallerList` | `StageTests.cs` | Caller's list unchanged | Passed (2026-10-13) |
| `SelectEligible_TrimsCustomer` | `StageTests.cs` | `"  Anna "` becomes `"Anna"` | Passed (2026-10-13) |
| `BuildSummary_TiedTotals_OrderedById` | `StageTests.cs` | R2 before R6 | Passed (2026-10-13) |
| `TwoPolicies_SameInput_DifferForLongRental` | `PolicyTests.cs` | R1: 15000 vs 12750 | Passed (2026-10-13) |
| `TwoPolicies_ShortRental_Equal` | `PolicyTests.cs` | R2: 12000 under both | Passed (2026-10-13) |
| `PriceAll_InvokesPolicyOncePerRequest` | `PolicyTests.cs` | Policy called exactly N times | Passed (2026-10-13) |
| `Discount_ExactlyFiveDays_Applies` | `PolicyTests.cs` | 5 days discounted, 4 days not | Passed (2026-10-13) |
| `FromName_Unknown_ReturnsNull` | `PolicyTests.cs` | `"vip"` gives `null` | Passed (2026-10-13) |

## Members

| Member | Logic | Tests | Docs |
|---|---|---|---|
| A | `RequestParser.Parse` | `ParserTests.cs` | Input and validation section |
| B | `Stages.SelectEligible`, `Stages.BuildSummary` | `StageTests.cs` | Stage 1 and Stage 3 section |
| C | `Pricing.PriceAll`, `PricingPolicies` | `PolicyTests.cs` | Stage 2 and policies section |
| D | `RentalWorkflow.Run`, `Program.cs`, `data/` | `WorkflowTests.cs`, `SampleData.cs` | This README, requirement map, test table |

## Layout

```
src/Rental.Domain/   pure core: models, pipeline stages, policies (no Console, no files)
src/Rental.App/      console app = the boundary: arguments, file reading, parsing, printing
tests/Rental.Tests/  xUnit tests (TestData/ for controlled sample files)
data/                sample input used by the demos
```
