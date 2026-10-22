# Festival stock

The festival page lists every active ingredient its menu uses. The admin enters how much of it is
available, sees how much the orders used, and is warned when it is used up or will run out within two
hours. "Verlauf" opens a chart of the use with two forecasts of when it runs out.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/FestivalIngredient.cs`: the available amount of one ingredient at one
  festival, `null` for unlimited. One row exists for every festival and ingredient.
- `IngredientStockService.ListAtFestivalAsync` builds one `Results/IngredientStockLevel` per
  ingredient from public facts on the same service: `UsageOf` (the series of
  `Results/IngredientUsagePointResult`), `RunsOutAtUtcSinceStart`, `RunsOutAtUtcFromLastTwoHours`
  (both through `RunsOutAtUtcAtTheMeasuredRate`), `RunsOutSoonAtUtc` and `StockWarningOf`.
  `SetAvailableAmountAsync` stores an amount and then runs `StockSoldOutMarker`, which
  `OrderAcceptanceService` also runs after every order.
- `Services/IngredientAmountComparison.cs`: `IsAtLeast`, the one comparison of ingredient amounts.
  The used-up check, both forecasts and the sold-out marker take it as a dependency.
- `Ports/IIngredientStockRepository.cs` with `Infrastructure/Repositories/IngredientStockRepository.cs`:
  `SumConsumedAmountsByOrderTimeAsync` (per ingredient and order time, as UTC) feeds the list;
  `SumConsumedAmountsAsync` (the total per ingredient) feeds only the sold-out marker. Both are the
  same join of order items and recipe lines.
- Routes: `GET /api/admin/festivals/{festivalId}/ingredients` and `PUT .../ingredients/{ingredientId}`
  in `Endpoints/AdminFestivalEndpoints.cs`, with `Handlers/AdminFestivalStockHandler.cs`.
- Wire records: `GastronomyApp.Contracts/Admin/Ingredients` (`AdminFestivalIngredientListView`,
  `AdminFestivalIngredientView`, `IngredientUsagePoint`, `SaveFestivalIngredientRequest`,
  `SavedFestivalIngredientView`) and `Contracts/Enums/StockWarning.cs`. Mapping:
  `Api/Mapping/AdminIngredientMapping.cs`.

Frontend, under `frontend/src/`:

- `admin/components/festivals/FestivalStock.vue`: the rows, in fixed grid columns (name, available
  amount on `IngredientAmountField.vue`, used amount, warning, "Verlauf"). `FestivalPage.vue` hands it
  the festival.
- `admin/components/festivals/FestivalStockChart.vue` puts the texts and the colours of
  `admin/composables/useChartTheme.ts` into the option that `admin/core/stockChartOption.ts` builds,
  and draws it with `admin/components/BaseChart.vue`. The time ticks come from
  `admin/core/timeAxisTicks.ts`. How the chart is drawn is on the `add-a-chart` page of `how-to`.
- Store: `admin/stores/festivalStock.ts`. Moments: `formatTimeOfDay`, `formatTimeTick` and
  `spansMoreThanOneDay` in `shared/core/moments.ts`. Amounts: `useIngredientAmountText` and
  `useAmountTextInEntryUnit` in `admin/composables/useIngredientAmountText.ts`, on
  `shownEntryUnitFor` and `displayedAmountIn` in `admin/core/ingredientAmounts.ts`. Texts:
  `admin.ingredients.labels.{available,used,runsOutAt,usedUp}`, `admin.ingredients.actions.history`
  and `admin.ingredients.history.*`.

## Invariants

- Stock is an estimate computed on every read. Nothing is stored and nothing polls; the store reloads
  on `ConfigurationChanged` and `OrdersChanged`. Stock never refuses an order.
- Amounts are compared through `IngredientAmountComparison.IsAtLeast` and never with `<` or `>=` on
  their own. Summing fractional portions in binary floating point misses the exact total by a few
  units in the last digit (eleven portions of 0,03 make 0,32999999999999996), so `IsAtLeast` allows a
  relative tolerance of `1e-10` of the larger amount, at least `1e-10`. Rounding drift of even
  100,000 portions stays far inside it, and a real difference an admin can type (a thousandth of a
  gram on a thousand kilograms) stays outside it. An article stays on sale while one portion is
  left, and an ingredient is used up once the used amount reaches the available one.
- The usage has one point per moment, carrying the cumulative amount after it (the items of one
  order make one point). It starts at the festival start and ends at now, held inside the festival
  period, so a running festival ends at now and one that is over ends at its end. An order outside
  the period, because the admin moved the dates after orders came in, counts at the nearest edge,
  and every order counted at one moment shares that moment's single point. A festival that has not
  started therefore has one point at its start carrying everything used.
- `usedAmount` is the last point of the usage, so both come from one read and can never disagree.
- `FestivalSchedule` decides whether the festival is running. Both forecasts are null unless it runs
  and an amount is set. Since the start: the rate over start to now. From the last two hours: the rate
  over the window from the later of the start and two hours ago, where an order exactly at the start
  of the window counts inside it; null when the window used nothing. Either is null when nothing is
  left, and null when its moment lies beyond the last date `DateTime` can hold (an absurd amount with
  a small rate), so such a forecast can never warn.
- `usedUp` when an amount is set and the used amount reaches it, running or not. Otherwise
  `runsOutSoon` when the earlier forecast falls before the festival end and at most two hours from
  now; `runsOutSoonAtUtc` carries that moment and is null in every other case. The forecast window and
  the warning horizon are two separate constants in `IngredientStockService`.
- The frontend never recomputes a forecast or the warning. A row shows text only for `runsOutSoon`
  and `usedUp`, in the `warning` colour with an alert icon, and the moment carries the weekday
  when the festival spans more than one local day.
- "Verlauf" is closed at first and open under one row at most: opening it under another row closes
  the first, and a second press closes it. The open row lives in the page only. An ingredient whose
  `usedAmount` is zero shows "Bisher wurde nichts verbraucht." instead of a chart.
- The chart shows only the period that matters: from a twentieth of the shown span before the first
  use (never before the festival start) to a twentieth after the later of now and the later forecast
  crossing (never after the festival end). When the usage holds no span of time at all, it shows the
  whole festival.
- The chart draws the used amount, the available amount (only when one is set), each forecast the
  laptop sent, and now while the festival runs. A forecast that reaches the amount on the chart names
  that moment there; one that reaches it later names its moment at the right edge
  (`admin.ingredients.history.runsOutBeyondTheChart`). Only the crossing the laptop warns about is
  marked as a warning.
- The readout under the pointer snaps to the usage points and states the real used amount at that
  moment, so it never reads a value between two orders that the straight line only suggests.
- The amount axis uses one unit, chosen from the axis maximum after rounding to what the screen
  shows: kilograms or litres once it reaches 1000, grams or millilitres below, pieces always.
- The time labels follow the length of the shown period, not of the festival: one day at most shows
  the time, up to three days the weekday and the time, longer the date alone, and no two neighbouring
  labels read the same, also in the night the clocks go back.

## Tests

- `GastronomyApp.Core.Tests/Services`: `IngredientStockServiceTest` (usage with both clamps and the
  shared points, both forecasts, the window and its start, a moment beyond the last date, fractional
  portions, the warning bounds), `StockSoldOutMarkerTest` (fractional portions too) and
  `IngredientAmountComparisonTest`.
- `GastronomyApp.Infrastructure.Tests/Repositories`: `IngredientStockRepositoryTest`.
- `GastronomyApp.Api.Tests/Endpoints`: `AdminFestivalIngredientEndpointsTest` (the wire shape, the
  `stockWarning` values, and an absurd amount that still answers the list).
- `frontend/tests`: `admin/stores/festivalStock.spec.ts`, `admin/core/stockChartOption.spec.ts`,
  `admin/core/timeAxisTicks.spec.ts`, `admin/core/chartTheme.spec.ts`,
  `admin/core/ingredientAmounts.spec.ts`, `admin/composables/useIngredientAmountText.spec.ts`,
  `admin/composables/useChartTheme.spec.ts`, `admin/components/BaseChart.spec.ts`,
  `admin/components/festivals/FestivalStock.spec.ts`, `FestivalStockChart.spec.ts` and
  `FestivalPage.stock.spec.ts`, `shared/core/moments.spec.ts`, `shared/theme.spec.ts`. Fixtures come
  from `anAdminFestivalIngredient` and `aFestival` in `tests/support/wireViews.ts`.
