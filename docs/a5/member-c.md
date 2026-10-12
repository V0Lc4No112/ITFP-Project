# Assignment 5: Member C, Stage 2 (Higher-Order Function) and Pricing Policies

**Area for the whole course:** Rules & states (A5 policies → A6 factory/transitions → A8 currying).
**Read first:** [../contracts.md](../contracts.md), especially the policy rules and the dataset table.

## What you build

**Stage 2:** file `src/Rental.Domain/Pipeline/Pricing.cs`

```csharp
public static ImmutableArray<PricedRental> PriceAll(ImmutableArray<RentalRequest> eligible, Func<RentalRequest, long> policy);
```
This is the **higher-order function** the assignment requires. It calls `policy(request)` once per request and returns `new PricedRental(request, total)` for each one.

**Policies:** file `src/Rental.Domain/Policies/PricingPolicies.cs`

```csharp
public static readonly Func<RentalRequest, long> Standard =
    r => r.DailyRateCents * r.Quantity * r.Days;

public static readonly Func<RentalRequest, long> LongRentalDiscount =
    r => { var b = Standard(r); return r.Days >= 5 ? b * 85 / 100 : b; };

public static Func<RentalRequest, long>? FromName(string name);  // "standard" / "discount" (ignore case), null if unknown
```

Rules:
- The policy is selected in `Program` (D wires it) and **passed as an argument**. There is no static mutable "current policy" field.
- An unknown policy name gives a clear message in `Program`, not a silent fallback to Standard. `FromName` returns `null` and D prints the error.
- The policies are `static readonly`, and each lambda only reads its argument. Neither captures anything mutable.

Expected values: R1 is 15000 under Standard and 12750 under Discount. R2 is 12000 under both.

## Your tests

File: `tests/Rental.Tests/PolicyTests.cs`

| Test | Asserts |
|---|---|
| `TwoPolicies_SameInput_DifferForLongRental` | R1: 15000 vs 12750 (the **two policies** case) |
| `TwoPolicies_ShortRental_Equal` | R2: 12000 under both, which shows the rule made the difference and not the pipeline |
| `PriceAll_InvokesPolicyOncePerRequest` | A policy that increments a counter is called exactly N times, which proves the delegate is really invoked |
| `Discount_ExactlyFiveDays_Applies` | 5 days gets the discount and 4 days does not (the boundary of the discount rule) |
| `FromName_Unknown_ReturnsNull` | `"vip"` gives `null` |

## Your README section (send to D)

- Where the higher-order function is (file and method) and the line where the delegate is invoked.
- How the policy is selected and passed in.
- Why no global state is involved.

## Git

```
git switch main && git pull
git switch -c a5/pricing-member-c
# implement + tests
dotnet test
git add src/Rental.Domain/Pipeline/Pricing.cs src/Rental.Domain/Policies tests/Rental.Tests/PolicyTests.cs
git commit -m "[A5] Implement pricing stage and two pricing policies"
git push -u origin a5/pricing-member-c
```
Then open a PR into `main` and ask another member to review it.

## Defense prep

- Point to the delegate invocation and explain what a higher-order function is.
- Show both policies on the same data and explain why the pipeline code didn't change.
- **Practice live change:** change the discount to 20% or the threshold to 7 days, update the assertions, and run the tests.
