# Equipment Rental Shop

Introduction to Functional Programming, 2026/2027. A group project built up across Assignments 5 to 8 and the final project.

The shop rents bikes, skis and camping gear. The program reads rental requests from a CSV file, keeps the eligible ones, prices them with a selectable pricing policy, and prints the quotes. Since Assignment 6 it also confirms each quote against a stock file and prints which bookings were accepted and what stock is left. Since Assignment 7 every step that can fail returns a `Result`, every lookup returns an `Option`, and the whole run is one `Map`/`Bind` chain whose outcome is handled once (see [Assignment 7](#assignment-7-exception-handling-in-fp)).

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

- .NET SDK 10 (pinned in `global.json`, rolls forward to the latest 10.0.x feature band). Checked with `dotnet --version` = `10.0.100`.

## Build, run, test

Working directory: the **repository root** (the folder with `Rental.slnx`). All paths below are relative to it.

```
dotnet build
dotnet test
dotnet run --project src/Rental.App -- data/requests.csv standard data/stock.csv
dotnet run --project src/Rental.App -- data/requests.csv discount data/stock.csv
dotnet run --project src/Rental.App -- data/requests.csv standard data/stock-missing-item.csv
dotnet run --project src/Rental.App -- data/requests.csv standard data/stock-sold-out.csv
dotnet run --project src/Rental.App -- data/requests.csv vip data/stock.csv
dotnet run --project src/Rental.App -- data/requests-invalid.csv standard data/stock.csv
dotnet run --project src/Rental.App -- data/requests.csv standard data/no-such-stock.csv
dotnet run --project src/Rental.App -- data/requests.csv standard data/stock-malformed.csv
dotnet test --filter Confirm_Twice
```

Usage: `dotnet run --project src/Rental.App -- <requests.csv> <standard|discount> <stock.csv>`. Since A6 the stock file is required. Wrong arguments print the usage. Since A7 every failure (invalid rows, unknown policy, missing or malformed file) prints `Rejected [Code]: ...` or `File problem [Code]: ...` on standard error and exits with code 1; nothing is printed on standard output. The expected screens are in [A7 demo commands](#a7-demo-commands-task-5).

### Demo commands

| Case | Command | Expected |
|---|---|---|
| Normal, Standard | `dotnet run --project src/Rental.App -- data/requests.csv standard data/stock.csv` | R3, R1, R2, R6, R5; grand total 790.00; count 5 |
| Normal, Discount | `dotnet run --project src/Rental.App -- data/requests.csv discount data/stock.csv` | R3, R1, R2, R6, R5; grand total 697.00; count 5 |
| Invalid input | `dotnet run --project src/Rental.App -- data/requests-invalid.csv standard data/stock.csv` | `Rejected [InvalidRequest]: ...` and every row error, the pipeline does not run, exit code 1 |
| Empty dataset | `dotnet run --project src/Rental.App -- data/requests-empty.csv standard data/stock.csv` | "No eligible rentals."; grand total 0.00; count 0; nothing booked; stock left BIKE-01=3, SKI-02=1, TENT-01=2 |
| Unknown policy | `dotnet run --project src/Rental.App -- data/requests.csv vip data/stock.csv` | `Rejected [UnknownPolicy]: Unknown policy 'vip'. Use 'standard' or 'discount'`, exit code 1 |

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

Example, `data/requests-invalid.csv` prints (A7 format, on standard error):

```
Rejected [InvalidRequest]: Requests file 'data/requests-invalid.csv' has 4 error(s); nothing was priced or booked
  Row 2: Days must be >= 1 (was -2)
  Row 3: Quantity must be an integer (was two)
  Row 4: Customer must not be blank
  Row 5: Category is invalid (was Boat)
```

## Pipeline

`Parse` (boundary) → `SelectEligible` → `PriceAll` → `BuildSummary` → print (boundary).

The composition is in `RentalWorkflow.Run` ([src/Rental.Domain/Pipeline/RentalWorkflow.cs](src/Rental.Domain/Pipeline/RentalWorkflow.cs)): each stage's output is the next stage's input, in one visible place. The boundary reads and parses the file, picks the policy from the command line, calls `Run`, and prints only after `Run` returns, so the core never touches the console or the file system and can be tested with plain records. Since A7 these boundary steps are chained in `AppWorkflow.Execute` (see [Assignment 7](#workflow-member-d)).

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

`PricingPolicies.FromName` maps `standard` and `discount` (case-insensitive) to these functions. Since A7 it returns `Option<Func<RentalRequest, long>>`: `None` for anything else, which `AppWorkflow.Execute` turns into an `UnknownPolicy` failure (it returned `null` in A5/A6).

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

> **Changed in A7.** `ConfirmResult` is deleted: `Confirm` now returns `Result<Reservation>` (`Ok(Reservation(Booking, Stock))` replaces `Accepted`, `Fail(Error)` replaces `Rejected(string)`, with the same message texts). `Stock.Available` returns `Option<int>` instead of `int?`. Rejected booking lines now also show the error code. This section is kept as the A6 record; the current design is in [Assignment 7](#assignment-7-exception-handling-in-fp).

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

The A5 quote list and grand totals (790.00 / 697.00) do not change. Output of the first command in A6 (since A7 the R5 line reads `R5     REJECTED   [NotEnoughStock] R5: ...`):

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
| `Create_ValidRequest_ReturnsValidWithSameData` | `FactoryTests.cs` | `Valid`, wrapping the same request |
| `Create_EachRule_RejectsWithReason` (7 cases) | `FactoryTests.cs` | Blank Id / Customer / ItemCode, Category 99, rate 0, quantity 0, days -2: one exact reason each |
| `Create_SeveralBadFields_ReportsAll` | `FactoryTests.cs` | Quantity 0 and Days 0: both reasons, not just the first |
| `Create_QuantityAndDaysOne_Accepted_Zero_Rejected` (3 cases) | `FactoryTests.cs` | 1 / 1 valid; 0 in either field invalid |
| `Create_FifteenDays_ReturnsValid` | `FactoryTests.cs` | 15 days is valid (eligibility is Stage 1's job) |
| `StockParser_BadRows_ReturnsInvalidWithRowNumbers` | `FactoryTests.cs` | `Row 2: Available must be an integer (was three)` |
| `Parse_NumericCategory_IsRejected` (4 cases) | `ParserTests.cs` | Category `0`, `1`, `2`, `7` rejected although they parse as enum numbers |
| `Invalid_WithNoReasons_Throws` | `StateTests.cs` | `Invalid([])` cannot be built |
| `Create_DuplicateOrNegative_RejectsWithReason` | `StateTests.cs` | `ItemCode BIKE-01 is listed twice`; `Available for BIKE-01 must be >= 0 (was -1)` |
| `Available_MissingItem_IsNone_ZeroIsSomeZero` | `StateTests.cs` | Unknown item has no record; 0 is a real count (bridged to `Option` in A7) |
| `Create_DoesNotKeepCallerList` | `StateTests.cs` | Changing the caller's list afterwards does not change the stock |
| `Confirm_EnoughStock_AcceptsAndReserves` | `TransitionTests.cs` | `B-R1`, BIKE-01 3 → 1 |
| `Confirm_QuantityEqualsAvailable_AcceptsAndLeavesZero` | `TransitionTests.cs` | 2 of 2 accepted, 0 left |
| `Confirm_NotEnough_RejectsAndKeepsStock` | `TransitionTests.cs` | R5 rejected; the input stock still has 1 tent |
| `Confirm_NoStockRecord_Rejects` | `TransitionTests.cs` | `R2: no stock record for SKI-02` |
| `Confirm_Twice_ReservesOnlyOnce` | `TransitionTests.cs` | Second confirm returns the same booking and the same stock (BIKE-01 stays 1) |

### A6 members

| Member | Logic | Tests | Docs |
|---|---|---|---|
| A | `ValidatedRental`, `StockParser`; the parser keeps text checks only | `FactoryTests.cs`, `Parse_NumericCategory_IsRejected` | Factory rules, stock file |
| B | `Validation<T>`, `BookingState`, `ConfirmResult`, `Stock` | `StateTests.cs` | Cases of the three unions |
| C | `BookingTransitions.Confirm` | `TransitionTests.cs` | Transitions and repeat rule |
| D | `RentalWorkflow.Run` on validated input, `Booking.ConfirmAll`, `Program.cs`, `data/stock*.csv` | `BookingTests.cs`, `WorkflowTests.cs`, `SampleData.cs` | This section, before and after, demo table |

## Assignment 7: exception handling in FP

A6 made illegal order impossible. A7 makes **failure explicit**. Every operation that can fail for a business reason returns `Result<T>` (`Ok` with a value, or `Fail` with an `Error` that has a code and a message). Every lookup that can find nothing returns `Option<T>` (`Some` or `None`), so a stock count of **0** (`Some(0)`) is never confused with "this shop has no such item" (`None`). The file system is touched in exactly one method, `DataFile.ReadLines`, which catches the file exceptions and turns them into `Fail`. The whole run is one `Bind`/`Map` chain, `AppWorkflow.Execute`, and `Program.cs` handles its final outcome **once**, with a single `switch`.

### Workflow (Member D)

[src/Rental.App/AppWorkflow.cs](src/Rental.App/AppWorkflow.cs)

```
args ─> AppWorkflow.Execute (D)
          RequestParser.Load(path)            A  Result<ImmutableArray<ValidatedRental>>   FileMissing | InvalidRequest
            .Bind(PricingPolicies.FromName    C  Option -> Result                          UnknownPolicy
                    .Map(RentalWorkflow.Run))    A5 pipeline, unchanged                   -> QuoteSummary
            .Bind(StockParser.Load(path)      A  Result<Stock>                             FileMissing | MalformedFile
                    .Map(Booking.ConfirmAll))  D  each quote: BookingTransitions.Confirm (C)
                                                   stock.Available(code)  Option<int>      None -> NoStockRecord
                                                   .Bind(EnsureEnough)                     NotEnoughStock
                                                   .Map(new Reservation)                -> Result<Reservation> per line
        .Map(Display.Render)                  D  Result<ImmutableArray<string>>
Program: switch { Ok -> print, exit 0 ; Fail -> Display.RenderError, exit 1 }   <- the only case handler
```

```csharp
public static Result<RunOutcome> Execute(string requestsPath, string policyName, string stockPath) =>
    RequestParser.Load(requestsPath)
        .Bind(rentals => PricingPolicies.FromName(policyName)
            .ToResult(() => new Error(ErrorCode.UnknownPolicy,
                $"Unknown policy '{policyName}'. Use 'standard' or 'discount'"))
            .Map(policy => RentalWorkflow.Run(rentals, policy, RentalWorkflow.DefaultMaxDays)))
        .Bind(summary => StockParser.Load(stockPath)
            .Map(stock => new RunOutcome(policyName.ToLowerInvariant(), summary, Booking.ConfirmAll(summary, stock))));
```

| Step | `Map` or `Bind` | Why |
|---|---|---|
| `RequestParser.Load` | start of the chain | Reading or validating the requests can fail: `FileMissing`, `FileUnreadable`, `InvalidRequest` |
| `FromName(...).ToResult(UnknownPolicy)` | `Bind` | Finding the policy **can fail**, so the step returns a `Result` |
| `RentalWorkflow.Run` | `Map` | The A5 pipeline **cannot fail** on validated input; it returns a plain `QuoteSummary` |
| `StockParser.Load` | `Bind` | Loading stock **can fail** (missing or malformed file) |
| `Booking.ConfirmAll` | `Map` | Confirming always produces a report. A rejected quote is a line **inside** the report, not a failure of the whole run (A6 rule: each quote is confirmed or rejected on its own) |
| `Display.Render` (in `Program`) | `Map` | Turning the outcome into lines cannot fail and does not print |

**Order:** requests → policy → stock. When a step fails, `Bind` and `Map` skip every later step and pass the same `Error` on. So an invalid requests file with an unknown policy and a missing stock file reports `InvalidRequest`, not `UnknownPolicy` or `FileMissing` (test `Execute_InvalidRequests_StopsTheChainAtTheFirstFailure`).

**No partial update.** Each load is all-or-nothing: one bad row means no rentals or no stock at all. Every value is immutable, and `ConfirmAll` passes new `Stock` values along instead of changing one. Nothing is written to disk, and `Program` prints only after the whole chain has finished, so a failure never leaves half a screen. Failures go to standard error; standard output stays empty.

**Display and Program.** [src/Rental.App/Display.cs](src/Rental.App/Display.cs) is pure: `Render(RunOutcome)` returns the screen as lines (`Ok` booking → `B-R3   confirmed      350.00`, `Fail` → `R5     REJECTED   [NotEnoughStock] R5: ...`) and `RenderError(Error)` returns `Rejected [Code]: message` for domain failures or `File problem [Code]: message` for operational ones, then one line per detail, indented two spaces. [src/Rental.App/Program.cs](src/Rental.App/Program.cs) checks the argument count, calls `AppWorkflow.Execute(...).Map(Display.Render)` and has exactly one `switch`: `Ok` prints to stdout and returns 0, `Fail` prints `RenderError` to stderr and returns 1. It has no `File.` calls and no `try`.

### Outcomes (Task 1)

Inputs are relative to the repository root; the requests file is `data/requests.csv` unless stated.

| # | Case | Input | Expected result | Exit |
|---|---|---|---|---|
| 1 | Normal | `standard`, `data/stock.csv` | `Ok`: B-R3, B-R1, B-R2, B-R6 confirmed; confirmed total **740.00**; stock left BIKE-01=0, SKI-02=0, TENT-01=1 | 0 |
| 2 | `NotEnoughStock` (R5) | `standard`, `data/stock.csv` | Line inside the `Ok` report: `[NotEnoughStock] R5: not enough TENT-01 (requested 2, available 1)` | 0 |
| 3 | `NoStockRecord` (R2, item missing) | `standard`, `data/stock-missing-item.csv` | Line: `[NoStockRecord] R2: no stock record for SKI-02`; confirmed total 620.00 | 0 |
| 4 | `NotEnoughStock` with 0 (R2, sold out) | `standard`, `data/stock-sold-out.csv` | Line: `[NotEnoughStock] R2: not enough SKI-02 (requested 1, available 0)`; confirmed total 620.00; SKI-02=0 still listed | 0 |
| 5 | `UnknownPolicy` | `vip`, `data/stock.csv` | `Fail`: `Unknown policy 'vip'. Use 'standard' or 'discount'` | 1 |
| 6 | `InvalidRequest` | `data/requests-invalid.csv`, `standard`, `data/stock.csv` | `Fail`: `Requests file 'data/requests-invalid.csv' has 4 error(s); nothing was priced or booked` + 4 row details | 1 |
| 7 | `FileMissing` | `standard`, `data/no-such-stock.csv` | `Fail`: `Cannot load stock file 'data/no-such-stock.csv': file not found` (`folder not found` when the folder is missing) | 1 |
| 8 | `MalformedFile` | `standard`, `data/stock-malformed.csv` | `Fail`: `Stock file 'data/stock-malformed.csv' is malformed; no stock was loaded` + `Row 3: Available must be an integer (was two)`, `Row 4: Expected 2 columns, found 1` | 1 |
| – | `FileUnreadable` (no demo file) | a file that exists but cannot be read, for example locked or access denied | `Fail`: `Cannot load <role> file '<path>': <exception message>` | 1 |

Domain failures: `InvalidRequest`, `UnknownPolicy`, `NoStockRecord`, `NotEnoughStock` (four; the brief needs three). Operational problems (`Error.IsOperational`): `FileMissing`, `FileUnreadable`, `MalformedFile`. There is no fallback: a file problem always stops the run.

### Which type means what (Member B)

[src/Rental.Domain/Results/](src/Rental.Domain/Results/)

| Type | Means | On a problem | Used by |
|---|---|---|---|
| `Validation<T>` (A6) | Outcome of a **factory**: is this input allowed to exist? | Collects **every** reason, so the user sees all bad rows at once | `ValidatedRental.Create`, `Stock.Create`, `StockParser.Parse` |
| `BookingState` (A6) | **Where** a booking is in its life: `Quoted` or `Confirmed` | Not a success/failure type; both cases are normal states | Input of `BookingTransitions.Confirm` |
| `Result<T>` | Outcome of a **workflow step** that can fail: `Ok(T Value)` or `Fail(Error Error)` | Stops at the **first** failure; `Map` and `Bind` skip the rest and keep the error | `DataFile.ReadLines`, both `Load`s, `Confirm`, `AppWorkflow.Execute` |
| `Option<T>` | Result of a **lookup**: `Some(T Value)` or `None` | `None` is not an error by itself; the caller decides with `ToResult` | `Stock.Available`, `PricingPolicies.FromName` |
| `Error` | `Code` (`ErrorCode`), `Message`, `Details` (one line per row problem), `IsOperational` | — | Every `Fail` |

`ToResult` is the bridge: `Validation<T>.ToResult(reasons => Error)` keeps every reason in `Details` (stock file → `MalformedFile`); `Option<T>.ToResult(() => Error)` turns `None` into a failure (`NoStockRecord`, `UnknownPolicy`) and `Some(0)` into `Ok(0)`.

Contradictory states cannot be built: the base constructors of `Result<T>` and `Option<T>` are private, so `Ok` and `Fail` (and `Some` and `None`) are the only cases; `Ok(null)`, `Fail(null)` and `Some(null)` throw; an `Error` with a blank message throws; the base types have no `.Value`, so callers must `switch` on the case.

### Lookups: present, absent, zero (Member C)

[src/Rental.Domain/States/Stock.cs](src/Rental.Domain/States/Stock.cs), [src/Rental.Domain/States/BookingTransitions.cs](src/Rental.Domain/States/BookingTransitions.cs), [src/Rental.Domain/Policies/PricingPolicies.cs](src/Rental.Domain/Policies/PricingPolicies.cs)

| Stock file | `stock.Available("SKI-02")` | `Confirm` for R2 (1 ski) |
|---|---|---|
| `stock.csv` (`SKI-02,1`) | `Some(1)` | `Ok(Reservation(B-R2, SKI-02 → 0))` |
| `stock-sold-out.csv` (`SKI-02,0`) | `Some(0)`: **present**, none left | `Fail(NotEnoughStock, "R2: not enough SKI-02 (requested 1, available 0)")` |
| `stock-missing-item.csv` (no SKI-02 row) | `None`: **absent** | `Fail(NoStockRecord, "R2: no stock record for SKI-02")` |

`Confirm` for a `Quoted` booking is a second, smaller chain:

```csharp
stock.Available(request.ItemCode)
    .ToResult(() => new Error(ErrorCode.NoStockRecord, $"{request.Id}: no stock record for {request.ItemCode}"))
    .Bind(available => EnsureEnough(request, available))     // NotEnoughStock when available < Quantity
    .Map(_ => new Reservation(new BookingState.Confirmed(quoted.Quote, $"B-{request.Id}"),
                              stock.Reserve(request.ItemCode, request.Quantity)));
```

`Reserve` runs only inside the last `Map`, after both checks passed, and returns a new `Stock`. Confirming a `Confirmed` booking returns `Ok` with the same booking and the same stock (A6 repeat rule). `PricingPolicies.FromName` returns `Some(Standard)`, `Some(LongRentalDiscount)` or `None`; `Execute` turns `None` into `UnknownPolicy`.

### File boundary (Member A)

[src/Rental.App/Io/DataFile.cs](src/Rental.App/Io/DataFile.cs), [StockParser.cs](src/Rental.App/Io/StockParser.cs), [RequestParser.cs](src/Rental.App/Io/RequestParser.cs)

`DataFile.ReadLines(string role, string path)` is the **only** method that touches the file system and the only `try/catch` in the project. It turns exceptions into values:

| Exception | Result |
|---|---|
| none | `Ok(lines)` |
| `FileNotFoundException` | `Fail(FileMissing, "Cannot load <role> file '<path>': file not found")` |
| `DirectoryNotFoundException` | `Fail(FileMissing, "Cannot load <role> file '<path>': folder not found")` |
| other `IOException`, `UnauthorizedAccessException` | `Fail(FileUnreadable, "Cannot load <role> file '<path>': <exception message>")` |

Any other exception is a programming error and is not caught. On top of it:

- `StockParser.Load(path)` = `ReadLines("stock", path).Bind(lines => Parse(lines).ToResult(MalformedFile))`. Every bad row becomes a detail; one bad row means no stock at all.
- `RequestParser.Load(path)` = `ReadLines("requests", path).Bind(...)`: `Ok(Valid)` when there are no errors, otherwise `Fail(InvalidRequest)` with every row error as a detail.

The domain (`src/Rental.Domain`) has no file access; it only sees the parsed values.

### A7 demo commands (Task 5)

Run from the repository root: `dotnet run --project src/Rental.App -- <requests.csv> <standard|discount> <stock.csv>`. Every screen below was checked against the program on 2026-10-25. Runs 1 to 4 print to standard output (exit 0); runs 5 to 8 print **nothing** on standard output and the lines below on standard error (exit 1).

| # | Path shown | Command arguments | Exit |
|---|---|---|---|
| 1 | Normal | `data/requests.csv standard data/stock.csv` | 0 |
| 2 | Normal, other policy | `data/requests.csv discount data/stock.csv` | 0 |
| 3 | Domain failure + **lookup absent** | `data/requests.csv standard data/stock-missing-item.csv` | 0 |
| 4 | **Lookup present with 0** (zero is not absent) | `data/requests.csv standard data/stock-sold-out.csv` | 0 |
| 5 | Domain failure | `data/requests.csv vip data/stock.csv` | 1 |
| 6 | Domain failure | `data/requests-invalid.csv standard data/stock.csv` | 1 |
| 7 | **Missing file** | `data/requests.csv standard data/no-such-stock.csv` | 1 |
| 8 | **Malformed file** | `data/requests.csv standard data/stock-malformed.csv` | 1 |

**1** `data/requests.csv standard data/stock.csv`
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
R5     REJECTED   [NotEnoughStock] R5: not enough TENT-01 (requested 2, available 1)
Confirmed total: 740.00
Stock left: BIKE-01=0, SKI-02=0, TENT-01=1
```

**2** `data/requests.csv discount data/stock.csv`
```
Policy: discount
R3   Cara           297.50
R1   Anna           127.50
R2   Ben            120.00
R6   Finn           102.00
R5   Eve             50.00
Grand total: 697.00
Count: 5

Bookings:
B-R3   confirmed      297.50
B-R1   confirmed      127.50
B-R2   confirmed      120.00
B-R6   confirmed      102.00
R5     REJECTED   [NotEnoughStock] R5: not enough TENT-01 (requested 2, available 1)
Confirmed total: 647.00
Stock left: BIKE-01=0, SKI-02=0, TENT-01=1
```

**3** `data/requests.csv standard data/stock-missing-item.csv` (quotes as in 1)
```
Bookings:
B-R3   confirmed      350.00
B-R1   confirmed      150.00
R2     REJECTED   [NoStockRecord] R2: no stock record for SKI-02
B-R6   confirmed      120.00
R5     REJECTED   [NotEnoughStock] R5: not enough TENT-01 (requested 2, available 1)
Confirmed total: 620.00
Stock left: BIKE-01=0, TENT-01=1
```

**4** `data/requests.csv standard data/stock-sold-out.csv` (quotes as in 1)
```
Bookings:
B-R3   confirmed      350.00
B-R1   confirmed      150.00
R2     REJECTED   [NotEnoughStock] R2: not enough SKI-02 (requested 1, available 0)
B-R6   confirmed      120.00
R5     REJECTED   [NotEnoughStock] R5: not enough TENT-01 (requested 2, available 1)
Confirmed total: 620.00
Stock left: BIKE-01=0, SKI-02=0, TENT-01=1
```

**5** `data/requests.csv vip data/stock.csv`
```
Rejected [UnknownPolicy]: Unknown policy 'vip'. Use 'standard' or 'discount'
```

**6** `data/requests-invalid.csv standard data/stock.csv`
```
Rejected [InvalidRequest]: Requests file 'data/requests-invalid.csv' has 4 error(s); nothing was priced or booked
  Row 2: Days must be >= 1 (was -2)
  Row 3: Quantity must be an integer (was two)
  Row 4: Customer must not be blank
  Row 5: Category is invalid (was Boat)
```

**7** `data/requests.csv standard data/no-such-stock.csv` (this file must not exist)
```
File problem [FileMissing]: Cannot load stock file 'data/no-such-stock.csv': file not found
```

**8** `data/requests.csv standard data/stock-malformed.csv`
```
File problem [MalformedFile]: Stock file 'data/stock-malformed.csv' is malformed; no stock was loaded
  Row 3: Available must be an integer (was two)
  Row 4: Expected 2 columns, found 1
```

Also checked: `data/requests-empty.csv standard data/stock.csv` prints `No eligible rentals.`, totals 0.00 and `Stock left: BIKE-01=3, SKI-02=1, TENT-01=2` (exit 0); `data/requests.csv standard nofolder/stock.csv` prints `File problem [FileMissing]: Cannot load stock file 'nofolder/stock.csv': folder not found` (exit 1); a wrong number of arguments prints the usage line (exit 1).

### A7 requirement map

| Brief | File | Method / type | Owner |
|---|---|---|---|
| Task 1: Result type, no contradictory states | `src/Rental.Domain/Results/Result.cs`, `Error.cs` | `Result<T>` (`Ok`/`Fail`, private base constructor, null guards, no `.Value` on the base type), `Error`, `ErrorCode` | B |
| Task 1: 3+ domain failures with useful errors | `States/BookingTransitions.cs`; `src/Rental.App/AppWorkflow.cs`; `Io/RequestParser.cs` | `Confirm` → `NoStockRecord`, `NotEnoughStock`; `Execute` → `UnknownPolicy`; `RequestParser.Load` → `InvalidRequest` | C, D, A |
| Task 1: README success/error per failing case | `README.md` | [Outcomes](#outcomes-task-1) | D |
| Task 2: Option for a real lookup, 0 is not absent | `States/Stock.cs`, `Policies/PricingPolicies.cs` | `Stock.Available : Option<int>`, `FromName : Option<Func<...>>` | C |
| Task 2: required item missing → Result failure | `States/BookingTransitions.cs`, `AppWorkflow.cs` | `Option.ToResult` in `ConfirmQuoted` (`NoStockRecord`) and in `Execute` (`UnknownPolicy`) | C, D |
| Task 2: Validation vs workflow state vs Result | `README.md` | [Which type means what](#which-type-means-what-member-b) | B |
| Task 3: Map + Bind, failure skips the function | `Results/Result.cs` | `Result.Map`, `Result.Bind` | B |
| Task 3: one real chain using both, handled once at the boundary | `AppWorkflow.cs`, `Program.cs` | `AppWorkflow.Execute` + `.Map(Display.Render)` + the single `switch`; second chain in `ConfirmQuoted` | D, C |
| Task 4: load CSV, missing + malformed at the boundary, no file access in the domain | `src/Rental.App/Io/` | `DataFile.ReadLines` (only `try/catch`), `StockParser.Load`, `RequestParser.Load` | A |
| Task 4: no partial update | `Io/`, `Pipeline/Booking.cs`, `Program.cs` | All-or-nothing loads; immutable values; `Program` prints only after the whole chain is `Ok`; nothing written to disk | A, D |
| Task 5: all demo paths, no unhandled exception | `data/`, `Program.cs` | The 8 [demo commands](#a7-demo-commands-task-5) | D |
| Tests (10+) and README | `tests/Rental.Tests/` | 69 tests, [A7 tests](#a7-tests) | all, D collects |

### A7 tests

Latest run, 2026-10-25, .NET SDK 10.0.100, from the repository root:

```
dotnet test
Passed!  - Failed:     0, Passed:    69, Skipped:     0, Total:    69 - Rental.Tests.dll (net10.0)
```

All file tests use `TempFolder` (A): a fresh folder under the system temp path, deleted in `Dispose`. No test reads `data/`. The A5/A6 tests above were bridged to the new types and still pass.

| Test | File | Case | Expected |
|---|---|---|---|
| `Map_Ok_PassesValueAndReturnsNewValue` | `ResultTests.cs` (B) | Map success | The function sees 15000 and the result is `Ok("150.00")` |
| `Bind_Ok_PassesValueAndKeepsNextResult` | `ResultTests.cs` (B) | Bind success | `Ok(3).Bind(TakeTwo)` is `Ok(1)`; `Ok(1).Bind(TakeTwo)` is the next step's own `Fail` |
| `Map_Fail_SkipsFunctionAndKeepsError` | `ResultTests.cs` (B) | Failure propagation | Counter stays 0; the same `Error` object comes out |
| `Bind_Fail_SkipsFunctionAndKeepsError` | `ResultTests.cs` (B) | Failure propagation | Counter stays 0; the same `Error` object comes out |
| `ContradictoryStates_CannotBeBuilt` | `ResultTests.cs` (B) | No contradictory states | `Ok(null)`, `Fail(null)`, `Some(null)` and a blank message all throw |
| `OptionToResult_SomeZeroIsOk_NoneUsesError` | `ResultTests.cs` (B) | 0 is not absent | `Some(0)` → `Ok(0)` without calling the error function; `None` → `NoStockRecord` |
| `ValidationToResult_InvalidKeepsEveryReason` | `ResultTests.cs` (B) | Validation → Result | Both stock reasons kept in `Details` |
| `ReadLines_MissingFile_FailsWithFileMissingAndPath` | `FileBoundaryTests.cs` (A) | Missing file | `FileMissing`, message contains the path, `IsOperational` |
| `ReadLines_MissingFolder_FailsWithFileMissing` | `FileBoundaryTests.cs` (A) | Missing folder | `FileMissing` |
| `StockLoad_ValidFile_ReturnsStock` | `FileBoundaryTests.cs` (A) | Load success | BIKE-01=3, SKI-02=0 |
| `StockLoad_MalformedFile_FailsWithEveryRowAndNoStock` | `FileBoundaryTests.cs` (A) | Malformed file, no partial update | `MalformedFile`, details `Row 3: ...`, `Row 4: ...` |
| `RequestsLoad_InvalidRow_FailsWithInvalidRequest` | `FileBoundaryTests.cs` (A) | Domain failure | `InvalidRequest`, not operational, detail `Row 3: Quantity must be >= 1 (was 0)` |
| `Available_ZeroIsPresent_UnknownIsNone` | `LookupTests.cs` (C) | Lookup present and absent | SKI-02 → `Some(0)`, BIKE-01 → `Some(3)`, BOAT-01 → `None` |
| `Confirm_ZeroAvailable_IsNotEnoughStock_NotNoStockRecord` | `LookupTests.cs` (C) | 0 is not absent | R2 with `SKI-02,0`: `NotEnoughStock`, `available 0` |
| `Confirm_DomainFailure_HasCodeAndMessage` (2 cases) | `LookupTests.cs` (C) | Domain failures | R2 → `NoStockRecord`; R5 → `NotEnoughStock`; exact messages; not operational |
| `FromName_Known_IsSome` (2 cases) | `LookupTests.cs` (C) | Lookup present | `standard`, `DISCOUNT` → `Some` |
| `Execute_SampleFiles_ProducesExpectedScreen` | `AppWorkflowTests.cs` (D) | Complete success | `Execute(...).Map(Display.Render)` is `Ok` and equals demo 1 line by line |
| `Execute_MissingStockFile_FailsAndLeavesFilesUnchanged` | `AppWorkflowTests.cs` (D) | Missing file, consistent state | `FileMissing`, message contains `stock`; the requests file is unchanged; no stock file was created |
| `Execute_MalformedStockFile_FailsWithoutBooking` | `AppWorkflowTests.cs` (D) | Malformed file, no partial update | `MalformedFile`; the stock file is unchanged |
| `Execute_UnknownPolicy_FailsBeforeStockIsRead` | `AppWorkflowTests.cs` (D) | Domain failure | Policy `vip` **and** a missing stock file: `UnknownPolicy` with the exact message, so the stock step was skipped |
| `Execute_InvalidRequests_StopsTheChainAtTheFirstFailure` | `AppWorkflowTests.cs` (D) | Failure propagation through the real chain | Bad row, policy `vip`, missing stock: `InvalidRequest` with detail `Row 2: Days must be >= 1 (was -2)` |

Test count (theory cases counted one by one): 16 (A5) + 30 (A6) + 7 (B) + 5 (A) + 6 (C) + 5 (D) = **69**.

### A7 members

| Member | Logic | Tests | Docs |
|---|---|---|---|
| A, Alimzhan Kabylkhanov | `DataFile.ReadLines` (the exception boundary), `StockParser.Load`, `RequestParser.Load`, `TempFolder` | `FileBoundaryTests.cs` (5) | File boundary |
| B, Daniyar Azizayev | `Result<T>`, `Error`, `ErrorCode`, `Option<T>`, `Map`, `Bind`, both `ToResult` | `ResultTests.cs` (7) | Which type means what |
| C, Mukhamejan Abdikerim | `Stock.Available : Option<int>`, `PricingPolicies.FromName : Option<...>`, `Confirm : Result<Reservation>`, `Reservation`; bridge edits in older tests | `LookupTests.cs` (6) | Lookups: present, absent, zero |
| D, Mereikhan Sholanbayev | `AppWorkflow.Execute` (the one chain), `Display`, new `Program.cs`, `Booking.ConfirmAll` on `Result`, `data/stock-sold-out.csv`, `data/stock-malformed.csv` | `AppWorkflowTests.cs` (5) | This section: workflow, outcomes, demos, requirement map, test table |

Build and run: see [Build, run, test](#build-run-test) (working directory = repository root, .NET SDK 10.0.100).

## A5 requirement map

| Assignment task | File | Method |
|---|---|---|
| Read input at the boundary | `src/Rental.App/Io/DataFile.cs` (since A7; `Program.cs` with `File.ReadAllLines` in A5) | `DataFile.ReadLines` |
| Parse and validate | `src/Rental.App/Io/RequestParser.cs` | `RequestParser.Parse` |
| Stage 1: filter | `src/Rental.Domain/Pipeline/Stages.cs` | `Stages.SelectEligible` |
| Stage 2: higher-order function | `src/Rental.Domain/Pipeline/Pricing.cs` | `Pricing.PriceAll` |
| Two policies passed as functions | `src/Rental.Domain/Policies/PricingPolicies.cs` | `Standard`, `LongRentalDiscount`, `FromName` |
| Stage 3: aggregate | `src/Rental.Domain/Pipeline/Stages.cs` | `Stages.BuildSummary` |
| Compose the pipeline | `src/Rental.Domain/Pipeline/RentalWorkflow.cs` | `RentalWorkflow.Run` |
| Print after the core returns | `src/Rental.App/Program.cs`, `src/Rental.App/Display.cs` | top-level statements, `Display.Render` |
| Sample data | `data/` | `requests.csv`, `requests-invalid.csv`, `requests-empty.csv` |
| Shared test records | `tests/Rental.Tests/SampleData.cs` | `SampleData.R1` to `R6`, `SampleData.All` |

## A5 tests

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
| `FromName_Unknown_IsNone` (was `FromName_Unknown_ReturnsNull` before A7) | `PolicyTests.cs` | `"vip"` gives `None` | Passed (2026-10-25) |

All 69 tests (A5, A6 and A7) pass on 2026-10-25; see [A7 tests](#a7-tests).

## A5 members

| Member | Logic | Tests | Docs |
|---|---|---|---|
| A | `RequestParser.Parse` | `ParserTests.cs` | Input and validation section |
| B | `Stages.SelectEligible`, `Stages.BuildSummary` | `StageTests.cs` | Stage 1 and Stage 3 section |
| C | `Pricing.PriceAll`, `PricingPolicies` | `PolicyTests.cs` | Stage 2 and policies section |
| D | `RentalWorkflow.Run`, `Program.cs`, `data/` | `WorkflowTests.cs`, `SampleData.cs` | This README, requirement map, test table |

## Layout

```
src/Rental.Domain/   pure core: models, pipeline stages, policies, states, Results/ (no Console, no files)
src/Rental.App/      console app = the boundary: Io/ (DataFile, parsers), AppWorkflow (the chain),
                     Display (screen lines), Program (arguments, the one switch, printing)
tests/Rental.Tests/  xUnit tests (TestData/ for controlled sample files)
data/                sample input used by the demos
```
