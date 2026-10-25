import type { LineSeriesOption } from 'echarts/charts'
import type {
  GridComponentOption,
  LegendComponentOption,
  TooltipComponentOption,
} from 'echarts/components'
import type { EChartsCoreOption } from 'echarts/core'
import type {
  AdminFestivalIngredientView,
  AdminFestivalView,
  IngredientUsagePoint,
} from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import type { TimeTickForm } from '../../shared/core/moments'
import type { ChartColours } from './chartTheme'
import { timeAxisTicksOf, timeTickFormOf } from './timeAxisTicks'

export type ForecastKind = 'fromLastTwoHours' | 'sinceStart'

export interface StockChartWording {
  used: string
  available: string
  forecastFromLastTwoHours: string
  forecastSinceStart: string
  now: string
  timeTickOf: (moment: Date, form: TimeTickForm) => string
  crossingOf: (moment: Date) => string
  crossingBeyondTheChartOf: (moment: Date) => string
  axisAmountOf: (amount: number) => string
  readoutOf: (moment: Date, usedAmount: number) => string
}

export interface FormattedAxisLabel {
  formatter: (value: number) => string
}

export interface TimeAxisLabel extends FormattedAxisLabel {
  hideOverlap: boolean
  customValues: number[]
}

export interface ShownPeriodTimeAxis {
  type: 'time'
  min: number
  max: number
  splitLine: { show: boolean }
  axisTick: { customValues: number[] }
  axisLabel: TimeAxisLabel
}

export interface AmountAxis {
  type: 'value'
  min: number
  max: number
  interval: number
  axisLabel: FormattedAxisLabel
}

export interface StockChartOption extends EChartsCoreOption {
  animation: boolean
  grid: GridComponentOption
  legend: LegendComponentOption
  tooltip: Omit<TooltipComponentOption, 'formatter'> & { formatter: (params: unknown) => string }
  xAxis: ShownPeriodTimeAxis
  yAxis: AmountAxis
  series: LineSeriesOption[]
}

interface ForecastBasis {
  availableAmount: number
  now: IngredientUsagePoint
  chartEndsAtMs: number
  warningAtMs: number | null
}

interface ShownPeriod {
  startsAtMs: number
  endsAtMs: number
}

const MOST_AMOUNT_STEPS = 4

const SMALLEST_AMOUNT_STEP = 1

const SPAN_SHARE_BEFORE_THE_FIRST_USE = 0.05

const SPAN_SHARE_AFTER_THE_LAST_MOMENT = 0.05

const AVAILABLE_DASHES = [10, 4, 2, 4]

const USED_AREA_OPACITY = 0.1

const LEGEND_KEY_WIDTH = 32

const LEGEND_ENTRY_GAP = 24

const CROSSING_MARKER_SIZE = 8

const NOW_LABEL_DISTANCE_BELOW_THE_AXIS = 26

const PLOT_TOP_UNDER_THE_LEGEND = 48

function amountStepFor(largestAmount: number): number {
  const roughStep = largestAmount / MOST_AMOUNT_STEPS
  const magnitude = 10 ** Math.floor(Math.log10(roughStep))
  const roundStep =
    [1, 2, 5].map((factor) => factor * magnitude).find((candidate) => candidate >= roughStep)
    ?? 10 * magnitude
  return Math.max(SMALLEST_AMOUNT_STEP, roundStep)
}

function usagePointNearest(usage: readonly IngredientUsagePoint[], momentMs: number): IngredientUsagePoint {
  return usage.reduce((nearest, point) =>
    Math.abs(Date.parse(point.atUtc) - momentMs) < Math.abs(Date.parse(nearest.atUtc) - momentMs)
      ? point
      : nearest,
  )
}

function usedLinePointsOf(usage: readonly IngredientUsagePoint[]): number[][] {
  const points = usage.map((point) => [Date.parse(point.atUtc), point.usedAmount])
  const firstUseIndex = usage.findIndex((point) => point.usedAmount > 0)
  if (firstUseIndex <= 0) {
    return points
  }
  const stillUnusedAtTheFirstUse = [Date.parse(usage[firstUseIndex].atUtc), 0]
  return [...points.slice(0, firstUseIndex), stillUnusedAtTheFirstUse, ...points.slice(firstUseIndex)]
}

function shownPeriodOf(
  usage: readonly IngredientUsagePoint[],
  festival: AdminFestivalView,
  forecastMomentsMs: readonly number[],
): ShownPeriod {
  const festivalStartsAtMs = Date.parse(festival.startsAtUtc)
  const festivalEndsAtMs = Date.parse(festival.endsAtUtc)
  const firstUseMs = Date.parse((usage.find((point) => point.usedAmount > 0) ?? usage[0]).atUtc)
  const lastMomentMs = Math.max(Date.parse(usage[usage.length - 1].atUtc), ...forecastMomentsMs)
  const spanMs = lastMomentMs - firstUseMs
  if (spanMs <= 0) {
    return { startsAtMs: festivalStartsAtMs, endsAtMs: festivalEndsAtMs }
  }
  return {
    startsAtMs: Math.max(
      festivalStartsAtMs,
      Math.round(firstUseMs - spanMs * SPAN_SHARE_BEFORE_THE_FIRST_USE),
    ),
    endsAtMs: Math.min(
      festivalEndsAtMs,
      Math.round(lastMomentMs + spanMs * SPAN_SHARE_AFTER_THE_LAST_MOMENT),
    ),
  }
}

function warningMomentOf(ingredient: AdminFestivalIngredientView): number | null {
  switch (ingredient.stockWarning) {
    case 'none':
    case 'usedUp':
      return null
    case 'runsOutSoon':
      return ingredient.runsOutSoonAtUtc === null ? null : Date.parse(ingredient.runsOutSoonAtUtc)
    default:
      return assertNever(ingredient.stockWarning)
  }
}

function forecastNameOf(kind: ForecastKind, wording: StockChartWording): string {
  switch (kind) {
    case 'fromLastTwoHours':
      return wording.forecastFromLastTwoHours
    case 'sinceStart':
      return wording.forecastSinceStart
    default:
      return assertNever(kind)
  }
}

function forecastLineTypeOf(kind: ForecastKind): 'dashed' | 'dotted' {
  switch (kind) {
    case 'fromLastTwoHours':
      return 'dashed'
    case 'sinceStart':
      return 'dotted'
    default:
      return assertNever(kind)
  }
}

function forecastColourOf(kind: ForecastKind, colours: ChartColours): string {
  switch (kind) {
    case 'fromLastTwoHours':
      return colours.data
    case 'sinceStart':
      return colours.muted
    default:
      return assertNever(kind)
  }
}

function crossingLabelPositionOf(kind: ForecastKind): 'top' | 'bottom' {
  switch (kind) {
    case 'fromLastTwoHours':
      return 'top'
    case 'sinceStart':
      return 'bottom'
    default:
      return assertNever(kind)
  }
}

function forecastSeriesOf(
  kind: ForecastKind,
  runsOutAtUtc: string,
  basis: ForecastBasis,
  wording: StockChartWording,
  colours: ChartColours,
): LineSeriesOption {
  const { availableAmount, now, chartEndsAtMs } = basis
  const nowMs = Date.parse(now.atUtc)
  const runsOutAtMs = Date.parse(runsOutAtUtc)
  const reachesTheAmountOnTheChart = runsOutAtMs <= chartEndsAtMs
  const lineColour = forecastColourOf(kind, colours)
  const isTheWarningPoint = runsOutAtMs === basis.warningAtMs
  const lastPoint = reachesTheAmountOnTheChart
    ? [runsOutAtMs, availableAmount]
    : [
        chartEndsAtMs,
        now.usedAmount
          + ((availableAmount - now.usedAmount) * (chartEndsAtMs - nowMs)) / (runsOutAtMs - nowMs),
      ]
  return {
    name: forecastNameOf(kind, wording),
    type: 'line',
    symbol: 'none',
    lineStyle: { type: forecastLineTypeOf(kind), color: lineColour },
    itemStyle: { color: lineColour },
    tooltip: { show: false },
    data: [[nowMs, now.usedAmount], lastPoint],
    markPoint: reachesTheAmountOnTheChart
      ? {
          symbol: 'circle',
          symbolSize: CROSSING_MARKER_SIZE,
          silent: true,
          itemStyle: { color: isTheWarningPoint ? colours.warning : lineColour },
          label: {
            position: crossingLabelPositionOf(kind),
            color: colours.ink,
            formatter: wording.crossingOf(new Date(runsOutAtMs)),
          },
          data: [{ name: forecastNameOf(kind, wording), coord: lastPoint }],
        }
      : {
          symbol: 'circle',
          symbolSize: 0,
          silent: true,
          label: {
            position: crossingLabelPositionOf(kind),
            align: 'right',
            color: colours.ink,
            formatter: wording.crossingBeyondTheChartOf(new Date(runsOutAtMs)),
          },
          data: [{ name: forecastNameOf(kind, wording), coord: lastPoint }],
        },
  }
}

export function stockChartOptionOf(
  ingredient: AdminFestivalIngredientView,
  festival: AdminFestivalView,
  wording: StockChartWording,
  colours: ChartColours,
): StockChartOption {
  const now = ingredient.usage[ingredient.usage.length - 1]
  const nowMs = Date.parse(now.atUtc)
  const availableAmount = ingredient.availableAmount
  const largestShown = Math.max(SMALLEST_AMOUNT_STEP, availableAmount ?? 0, now.usedAmount)
  const amountStep = amountStepFor(largestShown)
  const amountAxisMax = Math.ceil(largestShown / amountStep) * amountStep
  const sentForecasts: [ForecastKind, string | null][] = [
    ['fromLastTwoHours', ingredient.runsOutAtUtcFromLastTwoHours],
    ['sinceStart', ingredient.runsOutAtUtcSinceStart],
  ]
  const forecasts: [ForecastKind, string][] =
    availableAmount === null
      ? []
      : sentForecasts.flatMap(([kind, runsOutAtUtc]): [ForecastKind, string][] =>
          runsOutAtUtc === null ? [] : [[kind, runsOutAtUtc]],
        )
  const shown = shownPeriodOf(
    ingredient.usage,
    festival,
    forecasts.map(([, runsOutAtUtc]) => Date.parse(runsOutAtUtc)),
  )
  const timeTickForm = timeTickFormOf(shown.startsAtMs, shown.endsAtMs)
  const timeTicks = timeAxisTicksOf(shown.startsAtMs, shown.endsAtMs, timeTickForm)
  const usedSeries: LineSeriesOption = {
    name: wording.used,
    type: 'line',
    symbol: 'none',
    lineStyle: { type: 'solid', color: colours.data, cap: 'round', join: 'round' },
    itemStyle: { color: colours.data },
    areaStyle: { color: colours.data, opacity: USED_AREA_OPACITY },
    data: usedLinePointsOf(ingredient.usage),
    markLine: festival.isRunning
      ? {
          symbol: 'none',
          silent: true,
          lineStyle: { type: 'dotted', width: 1, color: colours.muted },
          label: {
            position: 'start',
            distance: NOW_LABEL_DISTANCE_BELOW_THE_AXIS,
            color: colours.muted,
            formatter: wording.now,
          },
          data: [{ xAxis: nowMs }],
        }
      : undefined,
  }
  const availableSeries: LineSeriesOption[] =
    availableAmount === null
      ? []
      : [
          {
            name: wording.available,
            type: 'line',
            symbol: 'none',
            lineStyle: { type: AVAILABLE_DASHES, color: colours.muted },
            tooltip: { show: false },
            data: [],
            markLine: {
              symbol: 'none',
              silent: true,
              lineStyle: { type: AVAILABLE_DASHES, width: 2, color: colours.muted },
              label: { position: 'end', color: colours.muted, formatter: wording.available },
              data: [{ yAxis: availableAmount }],
            },
          },
        ]
  const forecastSeries: LineSeriesOption[] =
    availableAmount === null
      ? []
      : forecasts.map(([kind, runsOutAtUtc]) =>
          forecastSeriesOf(
            kind,
            runsOutAtUtc,
            {
              availableAmount,
              now,
              chartEndsAtMs: shown.endsAtMs,
              warningAtMs: warningMomentOf(ingredient),
            },
            wording,
            colours,
          ),
        )
  return {
    animation: false,
    grid: { left: 104, right: 88, top: PLOT_TOP_UNDER_THE_LEGEND, bottom: 56 },
    legend: {
      top: 0,
      left: 0,
      selectedMode: false,
      itemWidth: LEGEND_KEY_WIDTH,
      itemGap: LEGEND_ENTRY_GAP,
      itemStyle: { opacity: 0 },
      data: [usedSeries, ...forecastSeries]
        .map((line) => line.name)
        .filter((name) => typeof name === 'string'),
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'line', snap: true },
      formatter: (params: unknown) => {
        const pointed: unknown = Array.isArray(params) ? params[0] : params
        if (
          typeof pointed !== 'object'
          || pointed === null
          || !('axisValue' in pointed)
          || typeof pointed.axisValue !== 'number'
        ) {
          return ''
        }
        const usagePoint = usagePointNearest(ingredient.usage, pointed.axisValue)
        return wording.readoutOf(new Date(usagePoint.atUtc), usagePoint.usedAmount)
      },
    },
    xAxis: {
      type: 'time',
      min: shown.startsAtMs,
      max: shown.endsAtMs,
      splitLine: { show: false },
      axisTick: { customValues: timeTicks },
      axisLabel: {
        hideOverlap: true,
        customValues: timeTicks,
        formatter: (value: number) => wording.timeTickOf(new Date(value), timeTickForm),
      },
    },
    yAxis: {
      type: 'value',
      min: 0,
      max: amountAxisMax,
      interval: amountStep,
      axisLabel: { formatter: (value: number) => wording.axisAmountOf(value) },
    },
    series: [usedSeries, ...availableSeries, ...forecastSeries],
  }
}
