# Add a chart to an admin screen

Admin charts are drawn by Apache ECharts. The shared pieces sit where the frontend keeps each kind of
file: the registration in `frontend/src/main.ts`, the theme in `admin/core/chartTheme.ts`, the
composable in `admin/composables/useChartTheme.ts` and the component in
`admin/components/BaseChart.vue`. The history chart of the festival stock
(`admin/components/festivals/FestivalStockChart.vue` with `admin/core/stockChartOption.ts`) is the
model to copy.

## Steps

1. **Register what the chart draws.** `main.ts` registers the ECharts pieces once, beside the
   Vuetify and i18n setup, with `use([...])` from `echarts/core`: today `LineChart`, the grid,
   tooltip, legend, mark line and mark point components and the `SVGRenderer`, then every entry of
   `CHART_THEMES` with `registerTheme`. A chart that needs another series type or component adds it
   to that list. Never import the full `echarts` bundle, and keep every import static.
2. **Take the colours from the theme.** `admin/core/chartTheme.ts` builds the `ChartColours` roles
   from the tokens in `shared/theme.ts` through `shared/core/themeColours.ts` (`themeColourOf`,
   `themeVariableOf`, `mutedInkOf`, which the table plan's colours use too), once per theme (`LIGHT_CHART_COLOURS`,
   `DARK_CHART_COLOURS`): `data` is the `chartData` teal, `ink` is `on-surface`, `muted` is
   `on-surface` at the medium emphasis opacity, and `warning` is the status token. `CHART_THEMES`
   holds the two ECharts themes built from the same roles (axis text and mark lines muted, axes, grid
   and tooltip border in `border-color`, the tooltip on `surface`, and `data` as the default series
   colour). `useChartTheme` picks both for the current device theme through
   `shared/composables/useThemedValue.ts`, which the table plan's colours use too: `BaseChart` takes its `name`, and
   the chart component hands its `colours` to the option function. The option sets each colour from
   a role, never from a hex value. A second data series needs its own token in `shared/theme.ts`
   first (frontend rule 6), checked with the `dataviz` skill's `scripts/validate_palette.js` against
   the light surface `#ffffff` and the dark surface `#252a32`.
3. **Build the option in core.** A plain function in `admin/core/<thing>ChartOption.ts` takes the
   wire views, a wording object (the texts and formatters, filled from i18n by the component) and the
   `ChartColours`, and returns the option. It imports ECharts only as types (`import type` from
   `echarts/charts`, `echarts/components` and `echarts/core`) and never imports Vue. Its return type
   is an interface that extends `EChartsCoreOption` and types the parts a spec calls, such as a
   formatter, as plain functions. Set `animation: false` when the chart reloads on a hub event, so a
   reload never replays the drawing.
4. **Draw it.** A component in the surface's `components/` computes the option from its props with
   `useI18n` and `useChartTheme`, and renders `<BaseChart :option="option" />` inside a wrapper with a
   fixed height. `BaseChart` follows the device theme through `useChartTheme`, draws SVG and resizes
   with its container. It knows nothing about any one chart, so a chart-specific need goes into the
   option, never into `BaseChart`.
5. **Texts.** Legend names, direct labels, axis labels and the tooltip sentence are keys of the
   feature's i18n section in `de.json` and `en.json`. Read `no-ai-slop` and `writing-ui-guidance`
   first.

## Conventions

The stock chart follows each of these; a new chart does the same.

- **Measured data.** `data` colour, a 2px solid line with round caps and joins, straight segments
  through the data points (no steps), at zero before the first value, and a wash under it in the same
  colour at 10 percent opacity. Look at the wash in both themes: it must read as a light tint, never
  a dark block.
- **Forecasts.** A forecast drawn from the measured data takes `data`, a second forecast `muted`; one
  is dashed, the other dotted, both 2px. A forecast ends where it reaches its target, or at the right
  edge when it reaches it later.
- **Reference lines.** An available amount or a similar limit is a `muted` dash and dot mark line
  (`[10, 4, 2, 4]`, 2px) labelled with its name at its right end (`markLine.label` with
  `position: 'end'`, and room for it in `grid.right`).
- **Moments at a point.** A moment worth reading sits at its point: an 8px `markPoint` circle in its
  line's colour, its label `top` for the first forecast and `bottom` for the second so the two never
  cover each other. A moment past the right edge is written at the edge as an arrow and the moment
  ("→ So., 19:35"), on a `markPoint` with `symbolSize: 0` and `align: 'right'`. Only the marker of a
  point that is itself a warning takes `warning`.
- **Text colour.** Every label takes `ink` or `muted`, never a line's colour and never `warning`, so
  the status colour stays on the marks. Warning text outside a chart takes the `warning` colour
  (`text-warning`).
- **Legend.** It names the measured data and every line without a label of its own, above the plot,
  with the line keys only (`itemStyle: { opacity: 0 }`) and no toggling (`selectedMode: false`).
- **The current moment.** A thin dotted `muted` mark line (`width: 1`) whose label sits under the
  time labels (`position: 'start'` with a `distance` that clears them, and room for it in
  `grid.bottom`). Never a bright line.
- **Readout.** `tooltip.trigger: 'axis'` with `axisPointer.snap: true`, and `tooltip: { show: false }`
  on every series but the measured data, so the pointer snaps only to its points. The formatter
  reads the data point nearest the pointer and states its real value, never a value the straight
  line only suggests between two points.
- **One unit per axis.** An amount axis chooses its unit once from the axis maximum with
  `shownEntryUnitFor` (`admin/core/ingredientAmounts.ts`, which rounds to what the screen shows
  first) and writes every label in it through the wording's formatter, which the component fills
  with `useAmountTextInEntryUnit`. Never format each label on its own, which mixes grams and
  kilograms on one axis.
- **Time axis.** Show only the period that matters, never the whole festival by default: from a
  fixed share of the shown span before the first data moment to the same kind of margin after the
  last moment worth seeing, clamped to the festival, with the shares named as constants
  (`shownPeriodOf` in `stockChartOption.ts` is the model). Take `timeTickFormOf(startsAtMs,
  endsAtMs)` and `timeAxisTicksOf(startsAtMs, endsAtMs, form)` from `admin/core/timeAxisTicks.ts`
  over that shown period and hand the moments to both `axisTick.customValues` and
  `axisLabel.customValues`, with `hideOverlap: true`. The labels come from
  `formatTimeTick(moment, language, form)` in `shared/core/moments.ts`: the time for a period of one
  day at most, the weekday and the time up to three days, the date alone beyond that. The ticks fall
  on whole local steps, so no two neighbouring labels read the same.

## Specs

- The option function gets the thorough spec; `admin/core/stockChartOption.spec.ts` is the model.
  Check which series and mark lines exist in each case, the line types, the colours, the legend
  names, the direct labels, the shown period, the axis unit, the tick moments, and call each
  formatter with a literal input. A wording object and a `ChartColours` object of plain stand-in
  strings (`'teal'`, `'muted ink'`) keep every expectation a literal. `admin/core/timeAxisTicks.spec.ts`
  covers the ticks and `admin/core/chartTheme.spec.ts` the colours and themes of both modes.
- A component or page spec mounts with `global: { stubs: { BaseChart: true } }` and checks the
  option the stub received through `getComponent(BaseChart).props('option')`, for example the
  localized legend names, a formatted label or the light theme's `chartData` value.
- `admin/components/BaseChart.spec.ts` replaces `vue-echarts` with a stand-in through `vi.mock` and
  checks what `BaseChart` hands it: the option, SVG drawing, `autoresize`, and the theme name before
  and after the app switches to dark (`useTheme().change('dark')` from a button in a small host
  component). `admin/composables/useChartTheme.spec.ts` covers the same switch for the composable.
- The test DOM has no layout and no canvas, so no spec asserts on what ECharts draws, where a label
  lands, whether two labels overlap, or how the chart resizes. To look at the real drawing while
  building it, `init(null, theme, { renderer: 'svg', ssr: true, width, height })` from `echarts/core`
  (after the same `use([...])` and `registerTheme` calls `main.ts` makes), then `setOption` and
  `renderToSVGString()`, renders the chart without a browser. That is a look, not a spec: write the
  file to a scratch folder and keep nothing of it in the repository.
