# Readable C#: what the owner wants, by example

## Decision

Code here is judged by one question: can a C# developer who has never seen it read the business rule
straight out of it? Every pair below is a real change from this repository's history (status history
and cancellation, October 2026). The "before" compiled, passed its tests and followed the written
rules; the owner still rejected it. Copy the "after".

The skill pages are a guideline. Where following one literally produces code like a "before" below,
write the "after" and say in the report which page you departed from and why (root rule 26).

## The principles

1. **The data answers questions about itself.** A value derived from a record is a member of that
   record or a check on its type, not a service method that is handed the record.
2. **Each piece of knowledge lives in exactly one place.** A rule written twice drifts apart; the
   second copy is where the next bug hides.
3. **A service holds actions, not calculations.** If a class loads nothing, writes nothing and refuses
   nothing, it is not a service, and injecting it only adds a hop.
4. **A rule you can only understand by reading the order of the cases needs a name.** Name the
   outcome, return it, and let the caller map it flatly.
5. **Let the database count.** Do not load lists of items into memory to count them.

## Pairs

### A calculation dressed up as a service

Before (`TableProgress`, deleted in 057c96e): injected, with a dependency, three hops for one colour,
two methods that differ only in the method group they pass, and the rule hidden in `Where`, `Count`
and `Contains`.

```csharp
public sealed class TableProgress
{
  private readonly OrderService _orderService;

  public ShareState HandOutProgressOf(ShownTable table) =>
    ProgressOfUnfinishedOrders(table.ItemProgressPerOrder.Select(_orderService.HandOutProgressOf));

  public ShareState SettlementProgressOf(ShownTable table) =>
    ProgressOfUnfinishedOrders(table.ItemProgressPerOrder.Select(_orderService.SettlementProgressOf));

  private ShareState ProgressOfUnfinishedOrders(IEnumerable<ShareState> shareOfEachOrder)
  {
    List<ShareState> sharesOfUnfinishedOrders = shareOfEachOrder.Where(share => share != ShareState.All).ToList();
    if (sharesOfUnfinishedOrders.Count == 0) return ShareState.All;
    if (sharesOfUnfinishedOrders.Contains(ShareState.Some)) return ShareState.Some;
    return ShareState.None;
  }
}
```

After (`Core/Results/UnfinishedOrders.cs`): the database counts, the record carries the answer, and
the rule reads as a sentence. "0 of 0 is done" falls out without a special case.

```csharp
public sealed record UnfinishedOrders(int Count, int PartlyDoneCount)
{
  public ShareState Progress
  {
    get
    {
      if (Count == 0)
        return ShareState.All;

      if (PartlyDoneCount > 0)
        return ShareState.Some;

      return ShareState.None;
    }
  }
}
```

### A `Func` parameter that switches behaviour

Before (`StationQueueService` at 3f5a517): one private loop takes a callback that decides what a
repeat means, so the station's rules live in three small methods the loop's reader never sees.

```csharp
return await AppendStatusToSelectedItemsAsync(orderItemIds, stationId, OrderItemState.HandedOut, KeepTheFirstHandOut, cancellationToken);

private async Task<ErrorOr<StationQueueView>> AppendStatusToSelectedItemsAsync(..., Func<OrderItem, OrderItemStatusChange, ErrorOr<Success>> acceptTheRepeat, ...)
```

After: the lifecycle names every outcome once, and the station maps each outcome to its answer in a
flat switch a reader can check line by line.

```csharp
switch (_itemLifecycle.JudgeChange(item, targetState))
{
  case OrderItemChangeVerdict.Allowed:
    toChange.Add(item);
    break;
  case OrderItemChangeVerdict.AlreadyInThatState:
    break;
  case OrderItemChangeVerdict.ItemIsCancelled:
    refusals.Add(Refusal.StationQueue.ItemWasCancelled(item.Id, item.ItemName));
    break;
  ...
}
```

### The same rule in two places

Before: `OrderItemLifecycle.CanAppendStatus` held the allowed transitions, and the station's
`RefusalOf` restated part of them in a switch whose `when` guards only made sense in their order. If
the two disagreed, `AppendStatus` threw and a volunteer got a server error instead of a sentence.

After (`OrderItemLifecycle.JudgeChange`): the transition table exists once, as a switch over a named
`StateChange(From, To)`, and returns a verdict. Callers translate the verdict; none of them knows the
table.

```csharp
return new StateChange(CurrentStateOf(item), targetState) switch
       {
         { From: OrderItemState.Open, To: OrderItemState.HandedOut } => OrderItemChangeVerdict.Allowed,
         { From: OrderItemState.HandedOut, To: OrderItemState.HandedOut } => OrderItemChangeVerdict.AlreadyInThatState,
         { From: OrderItemState.Cancelled, To: OrderItemState.Open or OrderItemState.HandedOut } => OrderItemChangeVerdict.ItemIsCancelled,
         ...
       };
```

### A chain written out every time

Before: each order check spelled `order.StationOrders.Any(stationOrder => stationOrder.Items.Any(...))`,
and conditions such as `!item.IsCancelled() && item.SettledAtUtc != null` were written inline.

After (`Core/Queries/OrderQueries.cs`, `OrderItemQueries.cs`): name the path once and name each
condition, so every check is one line of business words.

```csharp
[Projectable]
public static IEnumerable<OrderItem> Items(this Order order) => order.StationOrders.SelectMany(stationOrder => stationOrder.Items);

[Projectable]
public static bool IsPartlySettled(this Order order) => order.HasItemsToSettle() && order.Items().Any(item => item.IsStillOrdered() && item.IsSettled());
```

### A helper named after its mechanism

Before: `OrderService.ShareOfItemsNotCancelled(items, isFlagged)` described how it worked, took a
`Func` to serve two meanings, and hid an "all cancelled counts as done" special case.

After: named checks for each meaning (`HasItemsToHandOut`, `IsPartlyHandedOut`, `HasItemsToSettle`,
`IsPartlySettled`) and the counting moved to the database. Name a method after what it answers for
the business, never after how it computes it.

## Smells to stop at

- A class in `Core/Services` with no database access and no writes.
- A `Func` or method-group parameter that changes what a shared method means.
- A switch whose correctness depends on the order of its cases.
- The same condition, navigation chain or transition written in a second place.
- A list of items loaded only to be counted.
- A workaround line that reads like a bug (e.g. a Mapster identity mapping); find the setting that
  states the intent (`ShallowCopyForSameType`) or report it.

## Tests

None of its own: the pairs point at code that its own fixtures cover. The review uses this page:
a change that matches a "before" is a finding.
