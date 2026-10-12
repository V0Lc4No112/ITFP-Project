# Assignment 5: Exact Tasks per Member (and Topic Options)

## Part 1. Topic options

**Decision (2026-10-12): the group picked Topic 1, Equipment rental shop.**

Every topic below supports what A6 to A8 will add later: states (A6), a lookup by code (A7), categories to group by (A8) and a numeric calculation for the C library (A8).

| # | Topic | A5 workflow | Two policies | A6 states | A8 native C function |
|---|---|---|---|---|---|
| **1 (recommended)** | **Equipment rental shop** (bikes, skis, tents) | Rental requests → eligible requests → priced quotes | Standard vs long-rental discount | Pending → Confirmed → Returned | Late-return fee in cents |
| 2 | Gym class booking | Booking requests → eligible bookings → booking summary | Standard vs off-peak pricing | Requested → Confirmed / Waitlisted | Capacity utilisation % |
| 3 | Library loans and fines | Returned loans → overdue loans → fines summary | Standard vs student fine | Borrowed → Returned / Lost | Fine for N days overdue |
| 4 | Scholarship application scoring | Applications → eligible → ranked shortlist | GPA-weighted vs need-weighted score | Submitted → Approved / Rejected | Weighted score |
| 5 | Personal budget tracker | Transactions → period filter → category summary | Strict vs flexible budget limit | Draft → Committed month | Currency conversion in cents |

**Why rental is recommended:** it's the closest match to the course's own examples (quotes, booking confirmation, low-stock reports), so every assignment requirement fits it naturally. It uses only integers, with no dates needed in A5, which keeps the purity rules easy. It is also different enough from the delivery example to be your own work.

Everything below uses Topic 1. If you pick a different topic, keep the same split and rename the types.

---

## Part 2. Shared contract (agree on day 1, before anyone codes)

**Tooling:** .NET 10 (the current LTS; .NET 8 and 9 reach end of support on 10 Nov 2026), C#, xUnit, CSV input. Money is stored as integer cents (`long`), so there's no floating-point rounding.

**Input file `data/requests.csv`:**
```
Id,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember
```

**Types (in `src/Rental.Domain/Models/`):**
```csharp
public enum Category { Bike, Ski, Camping }

public sealed record RentalRequest(
    string Id, string Customer, string ItemCode, Category Category,
    long DailyRateCents, int Quantity, int Days, bool IsMember);

public sealed record PricedRental(RentalRequest Request, long TotalCents);

public sealed record QuoteSummary(
    ImmutableArray<PricedRental> Quotes, long GrandTotalCents, int Count);
```

**Business rules:**
- Boundary validation (Member A): every field must be present, `DailyRateCents > 0`, `Quantity >= 1`, `Days >= 1`, and `Category` must be a known value. Invalid rows get a clear error and are never silently fixed.
- Stage 1 rule (Member B): requests with `Days > 14` are not eligible (long rentals need manual approval). **14 is accepted, 15 is excluded.** Customer names are trimmed.
- Stage 2 policies (Member C):
  - `Standard`: `DailyRateCents × Quantity × Days`
  - `LongRentalDiscount`: the same base, then 15% off when `Days >= 5` (`base * 85 / 100`, integer division, so it rounds down to whole cents)
- Stage 3 rule (Member B): quotes ordered by `TotalCents` descending, then by `Id` ascending. Then the grand total and the count.

**Stage signatures (in `src/Rental.Domain/Pipeline/`):**
```csharp
// Stage 1 — Member B
static ImmutableArray<RentalRequest> SelectEligible(IReadOnlyList<RentalRequest> requests, int maxDays);
// Stage 2 — Member C (the higher-order function), in Pricing.cs
static ImmutableArray<PricedRental> PriceAll(ImmutableArray<RentalRequest> eligible, Func<RentalRequest, long> policy);
// Stage 3 — Member B
static QuoteSummary BuildSummary(ImmutableArray<PricedRental> priced);
// Composition — Member D
static QuoteSummary RentalWorkflow.Run(IReadOnlyList<RentalRequest> requests, Func<RentalRequest, long> policy, int maxDays);
```

**Dataset and expected results (Member D writes these BEFORE running the code):**

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
- `data/requests-invalid.csv` contains a negative `Days`, a non-numeric `Quantity`, an empty `Customer`, and an unknown `Category`.
- `data/requests-empty.csv` contains only the header row, which is a **valid empty dataset**.

---

## Part 3. Tasks per member

### Member D: setup, composition, dataset, README (start first)

**Day 1 (DONE 2026-10-12: skeleton committed to https://github.com/V0Lc4No112/ITFP-Project. The solution file is `Rental.slnx`, the .NET 10 default format):**
1. Create the GitHub repo and invite A, B and C. Add a `.gitignore` covering `bin/ obj/ .vs/ .idea/ *.user`.
2. Set up the solution:
   ```
   dotnet new globaljson --sdk-version <installed 10.x version>
   dotnet new sln -n Rental
   dotnet new classlib -n Rental.Domain -o src/Rental.Domain
   dotnet new console  -n Rental.App    -o src/Rental.App
   dotnet new xunit    -n Rental.Tests  -o tests/Rental.Tests
   dotnet sln add src/Rental.Domain src/Rental.App tests/Rental.Tests
   dotnet add src/Rental.App reference src/Rental.Domain
   dotnet add tests/Rental.Tests reference src/Rental.Domain src/Rental.App
   ```
3. Commit the Part 2 types and the stage signatures as stubs (`throw new NotImplementedException()`), using the branch `a5/contracts` and the commit message `[A5] Add solution, models and stage contracts`. Merge it, then everyone branches from `main`.

**Your logic:**
- `src/Rental.Domain/Pipeline/RentalWorkflow.cs`: `Run(...)` calls `SelectEligible` → `PriceAll` → `BuildSummary` in one visible chain.
- `src/Rental.App/Program.cs`: read the CSV path and policy name from the arguments (`dotnet run -- data/requests.csv standard|discount`). Read the file, call A's parser, pick the policy via C's `Policies.FromName`, call `Run`, then print the result. **All printing happens after `Run` returns.**
- `data/requests.csv`, `requests-invalid.csv` and `requests-empty.csv`, using exactly the Part 2 table.

**Your tests (`tests/Rental.Tests/WorkflowTests.cs`):**
- `NormalWorkflow_Standard_MatchesDocumentedResult`: order R3, R1, R2, R6, R5 and total 79000.
- `NormalWorkflow_Discount_MatchesDocumentedResult`: total 69700.
- `EmptyDataset_ReturnsEmptySummary`: count 0, total 0, no exception.

**README (you own it, the others send you their sections):** topic, users, team, input fields, rules, the expected results table, how to build/run/test, normal and failure demo commands, a map from each requirement to its file and method, the test table with the latest result, and the members section.

Branch: `a5/workflow-member-d`

---

### Member A: input models and boundary validation

**Your logic (`src/Rental.App/Io/RequestParser.cs`):**
```csharp
public sealed record ParseOutcome(ImmutableArray<RentalRequest> Valid, ImmutableArray<string> Errors);
public static ParseOutcome Parse(IReadOnlyList<string>? lines);
```
- If `lines` is `null` (missing input), return an error `"Input is missing"`. **Do not** treat it as empty.
- Header only is a valid empty dataset with no errors.
- Parse each row with `int.TryParse`, `long.TryParse`, `bool.TryParse` and `Enum.TryParse`. Every bad field produces an error naming the row and the field, for example `"Row 3: Days must be >= 1 (was -2)"`.
- `false` for `IsMember` is legitimate data, not "missing".
- Never use `!`, and never default an invalid value to 0. A row with any error does not go into `Valid`.
- Rule for the demo: if `Errors` is not empty, `Program` prints the errors and does not run the pipeline. That way no fabricated result appears. Agree on this with D.

**Your tests (`tests/Rental.Tests/ParserTests.cs`):**
- `Parse_Null_ReturnsMissingInputError`
- `Parse_HeaderOnly_ReturnsValidEmptyDataset`
- `Parse_InvalidFields_RejectsEachWithMessage`: negative days, non-numeric quantity, empty customer, unknown category.
- `Parse_IsMemberFalse_IsAcceptedAsValue`

**README section:** the input fields, the validation rules, and the missing vs empty vs invalid distinction.

Branch: `a5/validation-member-a`

---

### Member B: Stage 1 and Stage 3

**Your logic (`src/Rental.Domain/Pipeline/Stages.cs`):**
- `SelectEligible(requests, maxDays)`: keep `Days <= maxDays` and trim `Customer` using `r with { Customer = r.Customer.Trim() }`. Return a **new** `ImmutableArray` and never modify the input list.
- `BuildSummary(priced)`: order by `TotalCents` descending, then `Id` ascending, then calculate the grand total and the count. An empty input gives an empty summary.

**Your tests (`tests/Rental.Tests/StageTests.cs`):**
- `SelectEligible_ExactlyMaxDays_IsIncluded_OneMore_IsExcluded`: 14 is kept and 15 is dropped (the boundary case).
- `SelectEligible_DoesNotModifyCallerList`: pass a `List<RentalRequest>` and assert its count and items are identical afterwards (the purity case).
- `BuildSummary_TiedTotals_OrderedById`: R2 comes before R6.

**README section:** the input type, output type, rule and a worked example for Stage 1 and Stage 3. For example, R4 with 15 days goes into Stage 1 and does not come out.

Branch: `a5/stages-member-b`

---

### Member C: Stage 2 (higher-order function) and the two policies

**Your logic:**
- `src/Rental.Domain/Pipeline/Pricing.cs` → `PriceAll(eligible, policy)`: calls `policy(request)` for each request and returns `PricedRental` values. (It has its own file so it doesn't conflict with B's `Stages.cs`.)
- `src/Rental.Domain/Policies/PricingPolicies.cs`:
  ```csharp
  public static readonly Func<RentalRequest, long> Standard = r => r.DailyRateCents * r.Quantity * r.Days;
  public static readonly Func<RentalRequest, long> LongRentalDiscount = r => { var b = Standard(r); return r.Days >= 5 ? b * 85 / 100 : b; };
  public static Func<RentalRequest, long>? FromName(string name);  // "standard" / "discount", null if unknown
  ```
- The policy is chosen in `Program` and passed as an argument. There is **no static mutable "current policy" field**.
- An unknown policy name in `Program` produces a clear message, not a silent fallback to Standard.

**Your tests (`tests/Rental.Tests/PolicyTests.cs`):**
- `TwoPolicies_SameInput_DifferForLongRental`: R1 is 15000 under Standard and 12750 under Discount.
- `TwoPolicies_ShortRental_Equal`: R2 is 12000 under both (shows the rule, not the pipeline, made the difference).
- `PriceAll_InvokesPolicyOncePerRequest`: pass a policy that increments a counter and assert it equals the count. This proves the delegate is really called.
- `Discount_ExactlyFiveDays_Applies`: the boundary of the discount rule.

**README section:** where the higher-order function is, where the delegate is invoked, how the policy is selected, and why no global state is involved.

Branch: `a5/pricing-member-c`

---

## Part 4. Order of work

1. **D:** repo, solution and contracts PR, merged first.
2. **A, B and C in parallel**, each on their own branch with `[A5]` commits. They open PRs into `main`, and one other member reviews and runs `dotnet test` before merging.
3. **D:** wires `Program`, runs both policies against the expected table, and fills in the README with everyone's sections.
4. **Everyone:** run `dotnet test` (expect 14 passing tests, more than the 6 required). Tag `a5-submission`, build the ZIP with `git archive --format=zip -o A5.zip a5-submission`, and check it from a clean extraction.
5. **Defense rehearsal:** each member traces R1 through all three stages, explains their own part, and does one live change. Examples: A adds a new validation rule, B changes `maxDays` to 10, C changes the discount to 20%, D adds a sixth dataset row. Each change updates one assertion and the tests run green.

Requirement coverage check: normal workflow (D), empty dataset (D, A), boundary value (B, C), invalid and missing input (A), two policies (C), purity and stages (B). All six required cases are covered.
