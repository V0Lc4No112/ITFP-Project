# Assignment 5: Member B, Stage 1 (Eligibility) and Stage 3 (Summary)

**Area for the whole course:** Functional core (A5 stages → A7 Result/Map/Bind → A8 LINQ reports).
**Read first:** [../contracts.md](../contracts.md), especially the eligibility and ordering rules and the dataset table.

## What you build

File: `src/Rental.Domain/Pipeline/Stages.cs` (the signatures already exist with an empty body).

```csharp
public static ImmutableArray<RentalRequest> SelectEligible(IReadOnlyList<RentalRequest> requests, int maxDays);
public static QuoteSummary BuildSummary(ImmutableArray<PricedRental> priced);
```

**Stage 1, `SelectEligible`**
- Keep requests with `Days <= maxDays`. With `maxDays = 14`, 14 is kept and 15 is dropped.
- Trim the customer name with `r with { Customer = r.Customer.Trim() }`. This makes a new record and never changes the original.
- Return a **new** `ImmutableArray` (for example `requests.Where(...).Select(...).ToImmutableArray()`). Never modify the caller's list.
- Example: R4 (15 days) goes in and does not come out.

**Stage 3, `BuildSummary`**
- Order by `TotalCents` descending, then `Id` ascending (`OrderByDescending(...).ThenBy(..., StringComparer.Ordinal)`).
- `GrandTotalCents` is the sum of all totals, and `Count` is the number of quotes.
- An empty input gives `QuoteSummary([], 0, 0)` with no exception.
- Example: R2 and R6 both cost 12000, so R2 comes before R6.

Both functions must be pure: no `Console`, no files, no static state.

## Your tests

File: `tests/Rental.Tests/StageTests.cs`

| Test | Asserts |
|---|---|
| `SelectEligible_ExactlyMaxDays_IsIncluded_OneMore_IsExcluded` | Days 14 is kept and days 15 is dropped (the **boundary** case) |
| `SelectEligible_DoesNotModifyCallerList` | Pass a `List<RentalRequest>` that includes an untrimmed name. Afterwards its count and items are unchanged (the **purity** case) |
| `SelectEligible_TrimsCustomer` | `"  Anna "` becomes `"Anna"` in the output |
| `BuildSummary_TiedTotals_OrderedById` | R2 comes before R6; the grand total and count are correct |

## Your README section (send to D)

For each of your two stages, give the input type, output type, rule, and one worked example using the dataset rows.

## Git

```
git switch main && git pull
git switch -c a5/stages-member-b
# implement + tests
dotnet test
git add src/Rental.Domain/Pipeline/Stages.cs tests/Rental.Tests/StageTests.cs
git commit -m "[A5] Implement eligibility and summary stages"
git push -u origin a5/stages-member-b
```
Then open a PR into `main` and ask another member to review it.

## Defense prep

- Trace R1 and R4 through Stage 1, and the priced results through Stage 3.
- Explain why your stages have no side effects and how the purity test proves it.
- **Practice live change:** change `maxDays` to 10 and update the boundary assertion, or change the tie-breaker to `Customer`.
