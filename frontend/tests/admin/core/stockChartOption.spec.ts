import { describe, expect, it } from 'vitest'
import { stockChartOptionOf, type StockChartWording } from '../../../src/admin/core/stockChartOption'
import type { ChartColours } from '../../../src/admin/core/chartTheme'
import { aFestival, anAdminFestivalIngredient } from '../../support/wireViews'

const AT_10_00_UTC = 1_784_368_800_000

const AT_13_00_UTC = 1_784_379_600_000

const AT_14_20_UTC = 1_784_384_400_000

const AT_15_00_UTC = 1_784_386_800_000

const AT_18_00_UTC = 1_784_397_600_000

const AT_22_00_UTC = 1_784_412_000_000

const NEXT_DAY_AT_02_00_UTC = 1_784_426_400_000

const WORDING: StockChartWording = {
  used: 'Verbraucht',
  available: 'Verfügbar',
  forecastFromLastTwoHours: 'Vorhersage aus den letzten 2 Stunden',
  forecastSinceStart: 'Vorhersage seit Festbeginn',
  now: 'Jetzt',
  timeTickOf: (moment, form) => `${form} ${moment.toISOString()}`,
  crossingOf: (moment) => `crossing ${moment.toISOString()}`,
  crossingBeyondTheChartOf: (moment) => `beyond ${moment.toISOString()}`,
  axisAmountOf: (amount, entryUnit) => `${amount} ${entryUnit}`,
  readoutOf: (moment, usedAmount) => `${moment.toISOString()} used ${usedAmount}`,
}

const COLOURS: ChartColours = {
  data: 'teal',
  ink: 'ink',
  muted: 'muted ink',
  warning: 'yellow',
}

const SUMMER_FESTIVAL = aFestival({
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
  isRunning: true,
})

const FOUR_DAY_FESTIVAL = aFestival({
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-22T10:00:00Z',
  isRunning: true,
})

const FLOUR = anAdminFestivalIngredient({
  unit: 'gram',
  availableAmount: 2000,
  usedAmount: 1000,
  usage: [
    { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
    { atUtc: '2026-07-18T13:00:00Z', usedAmount: 500 },
    { atUtc: '2026-07-18T15:00:00Z', usedAmount: 1000 },
    { atUtc: '2026-07-18T18:00:00Z', usedAmount: 1000 },
  ],
  runsOutAtUtcFromLastTwoHours: '2026-07-18T22:00:00Z',
  runsOutAtUtcSinceStart: '2026-07-19T10:00:00Z',
})

const FLOUR_RUNNING_OUT_SOON = anAdminFestivalIngredient({
  ...FLOUR,
  runsOutAtUtcSinceStart: '2026-07-18T23:00:00Z',
  stockWarning: 'runsOutSoon',
  runsOutSoonAtUtc: '2026-07-18T22:00:00Z',
})

function optionOf(ingredient = FLOUR, festival = SUMMER_FESTIVAL) {
  return stockChartOptionOf(ingredient, festival, WORDING, COLOURS)
}

function seriesNamed(name: string, ingredient = FLOUR, festival = SUMMER_FESTIVAL) {
  return optionOf(ingredient, festival).series.find((line) => line.name === name)
}

function shownPeriodOf(ingredient = FLOUR, festival = SUMMER_FESTIVAL): string[] {
  const { xAxis } = optionOf(ingredient, festival)
  return [new Date(xAxis.min).toISOString(), new Date(xAxis.max).toISOString()]
}

describe('the history chart of an ingredient', () => {
  it('divides the amount axis into round steps from zero up to the available amount', () => {
    expect(optionOf().yAxis).toMatchObject({ type: 'value', min: 0, max: 2000, interval: 500 })
  })

  it('shows at least one whole unit on the amount axis when nothing is available and nothing was used', () => {
    const option = optionOf(
      anAdminFestivalIngredient({
        availableAmount: null,
        usedAmount: 0,
        usage: [
          { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
          { atUtc: '2026-07-18T18:00:00Z', usedAmount: 0 },
        ],
      }),
    )

    expect(option.yAxis).toMatchObject({ min: 0, max: 1, interval: 1 })
  })

  it('draws the used amount as a solid line straight through every point, at zero until the first use', () => {
    expect(seriesNamed('Verbraucht')).toMatchObject({
      type: 'line',
      symbol: 'none',
      lineStyle: { type: 'solid', cap: 'round', join: 'round' },
      data: [
        [AT_10_00_UTC, 0],
        [AT_13_00_UTC, 0],
        [AT_13_00_UTC, 500],
        [AT_15_00_UTC, 1000],
        [AT_18_00_UTC, 1000],
      ],
    })
  })

  it('draws the available amount as a dash and dot mark line across the chart', () => {
    expect(seriesNamed('Verfügbar')).toMatchObject({
      data: [],
      lineStyle: { type: [10, 4, 2, 4] },
      markLine: { lineStyle: { type: [10, 4, 2, 4] }, data: [{ yAxis: 2000 }] },
    })
  })

  it('names the available line at its right end', () => {
    expect(seriesNamed('Verfügbar')?.markLine).toMatchObject({
      label: { position: 'end', formatter: 'Verfügbar' },
    })
  })

  it('draws the forecast from the last two hours dashed from now to where it reaches the available amount', () => {
    expect(seriesNamed('Vorhersage aus den letzten 2 Stunden')).toMatchObject({
      lineStyle: { type: 'dashed' },
      data: [
        [AT_18_00_UTC, 1000],
        [AT_22_00_UTC, 2000],
      ],
    })
  })

  it('names the moment a forecast reaches the available amount at that point when it falls on the chart', () => {
    expect(seriesNamed('Vorhersage aus den letzten 2 Stunden')?.markPoint).toMatchObject({
      symbolSize: 8,
      label: { position: 'top', formatter: 'crossing 2026-07-18T22:00:00.000Z' },
      data: [{ coord: [AT_22_00_UTC, 2000] }],
    })
  })

  it('draws the forecast since the start dotted up to the right edge when it reaches the amount later', () => {
    expect(seriesNamed('Vorhersage seit Festbeginn')).toMatchObject({
      lineStyle: { type: 'dotted' },
      data: [
        [AT_18_00_UTC, 1000],
        [NEXT_DAY_AT_02_00_UTC, 1500],
      ],
    })
  })

  it('writes the later moment at the right edge with an arrow and no marker', () => {
    expect(seriesNamed('Vorhersage seit Festbeginn')?.markPoint).toMatchObject({
      symbolSize: 0,
      label: { position: 'bottom', align: 'right', formatter: 'beyond 2026-07-19T10:00:00.000Z' },
      data: [{ coord: [NEXT_DAY_AT_02_00_UTC, 1500] }],
    })
  })

  it('names the moment of the forecast since the start below the line so the two moments never cover each other', () => {
    expect(seriesNamed('Vorhersage seit Festbeginn', FLOUR_RUNNING_OUT_SOON)?.markPoint).toMatchObject({
      label: { position: 'bottom', formatter: 'crossing 2026-07-18T23:00:00.000Z' },
    })
  })

  it('names the used amount and the two forecasts in the legend, in the order they are drawn', () => {
    const option = optionOf()

    expect(option.legend).toMatchObject({
      selectedMode: false,
      data: ['Verbraucht', 'Vorhersage aus den letzten 2 Stunden', 'Vorhersage seit Festbeginn'],
    })
    expect(option.series.map((line) => line.name)).toEqual([
      'Verbraucht',
      'Verfügbar',
      'Vorhersage aus den letzten 2 Stunden',
      'Vorhersage seit Festbeginn',
    ])
  })

  it('draws neither the available line nor a forecast for an ingredient without an amount', () => {
    const option = optionOf({
      ...FLOUR,
      availableAmount: null,
      runsOutAtUtcFromLastTwoHours: null,
      runsOutAtUtcSinceStart: null,
    })

    expect(option.series.map((line) => line.name)).toEqual(['Verbraucht'])
    expect(option.legend.data).toEqual(['Verbraucht'])
  })

  it('draws only the forecast the laptop sent', () => {
    const option = optionOf({ ...FLOUR, runsOutAtUtcFromLastTwoHours: null })

    expect(option.series.map((line) => line.name)).toEqual([
      'Verbraucht',
      'Verfügbar',
      'Vorhersage seit Festbeginn',
    ])
    expect(option.legend.data).toEqual(['Verbraucht', 'Vorhersage seit Festbeginn'])
  })

  it('marks now with a thin dotted line at the last usage point while the festival runs', () => {
    expect(seriesNamed('Verbraucht')?.markLine).toMatchObject({
      lineStyle: { type: 'dotted', width: 1 },
      data: [{ xAxis: AT_18_00_UTC }],
    })
  })

  it('names now below the plot, under the time labels', () => {
    expect(seriesNamed('Verbraucht')?.markLine).toMatchObject({
      label: { position: 'start', distance: 26, formatter: 'Jetzt' },
    })
  })

  it('marks no now once the festival has ended', () => {
    expect(seriesNamed('Verbraucht', FLOUR, { ...SUMMER_FESTIVAL, isRunning: false })?.markLine).toBeUndefined()
  })
})

describe('the period the history chart shows', () => {
  it('runs from a twentieth of the span before the first use to a twentieth after the later crossing', () => {
    expect(shownPeriodOf(FLOUR_RUNNING_OUT_SOON)).toEqual([
      '2026-07-18T12:30:00.000Z',
      '2026-07-18T23:30:00.000Z',
    ])
  })

  it('never starts before the festival starts', () => {
    const usedSoonAfterTheStart = anAdminFestivalIngredient({
      ...FLOUR_RUNNING_OUT_SOON,
      usage: [
        { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
        { atUtc: '2026-07-18T10:30:00Z', usedAmount: 500 },
        { atUtc: '2026-07-18T18:00:00Z', usedAmount: 1000 },
      ],
    })

    expect(shownPeriodOf(usedSoonAfterTheStart)[0]).toBe('2026-07-18T10:00:00.000Z')
  })

  it('never ends after the festival ends, even when a forecast reaches the amount later', () => {
    expect(shownPeriodOf()[1]).toBe('2026-07-19T02:00:00.000Z')
  })

  it('ends a twentieth of the span after now when the laptop sent no forecast', () => {
    expect(
      shownPeriodOf({ ...FLOUR, runsOutAtUtcFromLastTwoHours: null, runsOutAtUtcSinceStart: null }),
    ).toEqual(['2026-07-18T12:45:00.000Z', '2026-07-18T18:15:00.000Z'])
  })

  it('ends at the end of a festival that is over', () => {
    const usedAtAnEndedFestival = anAdminFestivalIngredient({
      ...FLOUR,
      usage: [
        { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
        { atUtc: '2026-07-18T13:00:00Z', usedAmount: 500 },
        { atUtc: '2026-07-19T02:00:00Z', usedAmount: 500 },
      ],
      runsOutAtUtcFromLastTwoHours: null,
      runsOutAtUtcSinceStart: null,
    })

    expect(shownPeriodOf(usedAtAnEndedFestival, { ...SUMMER_FESTIVAL, isRunning: false })).toEqual([
      '2026-07-18T12:21:00.000Z',
      '2026-07-19T02:00:00.000Z',
    ])
  })

  it('shows the whole festival when the usage holds no span of time', () => {
    const usedAtAFestivalMovedIntoTheFuture = anAdminFestivalIngredient({
      ...FLOUR,
      usedAmount: 300,
      usage: [{ atUtc: '2026-07-18T10:00:00Z', usedAmount: 300 }],
      runsOutAtUtcFromLastTwoHours: null,
      runsOutAtUtcSinceStart: null,
    })

    expect(shownPeriodOf(usedAtAFestivalMovedIntoTheFuture, { ...SUMMER_FESTIVAL, isRunning: false })).toEqual([
      '2026-07-18T10:00:00.000Z',
      '2026-07-19T02:00:00.000Z',
    ])
  })
})

describe('the colours of the history chart', () => {
  it('draws the used amount and the light wash under it in the data colour', () => {
    expect(seriesNamed('Verbraucht')).toMatchObject({
      lineStyle: { color: 'teal' },
      areaStyle: { color: 'teal', opacity: 0.1 },
    })
  })

  it('draws the forecast from the last two hours in the data colour', () => {
    expect(seriesNamed('Vorhersage aus den letzten 2 Stunden')).toMatchObject({
      lineStyle: { color: 'teal' },
      itemStyle: { color: 'teal' },
    })
  })

  it('keeps the available line, its label and the forecast since the start muted', () => {
    expect(seriesNamed('Verfügbar')?.markLine).toMatchObject({
      lineStyle: { color: 'muted ink' },
      label: { color: 'muted ink' },
    })
    expect(seriesNamed('Vorhersage seit Festbeginn')).toMatchObject({
      lineStyle: { color: 'muted ink' },
      itemStyle: { color: 'muted ink' },
    })
  })

  it('keeps now and its label muted', () => {
    expect(seriesNamed('Verbraucht')?.markLine).toMatchObject({
      lineStyle: { color: 'muted ink' },
      label: { color: 'muted ink' },
    })
  })

  it('draws a crossing point in the colour of its line with its moment in ink while the stock lasts', () => {
    expect(seriesNamed('Vorhersage aus den letzten 2 Stunden')?.markPoint).toMatchObject({
      itemStyle: { color: 'teal' },
      label: { color: 'ink' },
    })
  })

  it('draws only the marker of the crossing point the laptop warns about in the warning colour', () => {
    expect(
      seriesNamed('Vorhersage aus den letzten 2 Stunden', FLOUR_RUNNING_OUT_SOON)?.markPoint,
    ).toMatchObject({
      itemStyle: { color: 'yellow' },
      label: { color: 'ink' },
    })
  })

  it('leaves the other crossing point in the colour of its line when the laptop warns about one', () => {
    expect(seriesNamed('Vorhersage seit Festbeginn', FLOUR_RUNNING_OUT_SOON)?.markPoint).toMatchObject({
      itemStyle: { color: 'muted ink' },
      label: { color: 'ink' },
    })
  })

  it('leaves a crossing point in the colour of its line when the laptop says the stock is used up', () => {
    expect(
      seriesNamed('Vorhersage aus den letzten 2 Stunden', {
        ...FLOUR,
        stockWarning: 'usedUp',
        runsOutSoonAtUtc: null,
      })?.markPoint,
    ).toMatchObject({
      itemStyle: { color: 'teal' },
      label: { color: 'ink' },
    })
  })

  it('writes the moment at the right edge in ink', () => {
    expect(seriesNamed('Vorhersage seit Festbeginn')?.markPoint).toMatchObject({ label: { color: 'ink' } })
  })
})

describe('the amount axis of the history chart', () => {
  it('writes every amount in kilograms once the axis reaches 1000 grams', () => {
    const option = optionOf()

    expect(option.yAxis.axisLabel.formatter(500)).toBe('500 kilogram')
    expect(option.yAxis.axisLabel.formatter(2000)).toBe('2000 kilogram')
  })

  it('writes every amount in grams while the axis stays below 1000 grams', () => {
    const option = optionOf(
      anAdminFestivalIngredient({
        unit: 'gram',
        availableAmount: 800,
        usedAmount: 300,
        usage: [
          { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
          { atUtc: '2026-07-18T18:00:00Z', usedAmount: 300 },
        ],
      }),
    )

    expect(option.yAxis.max).toBe(800)
    expect(option.yAxis.axisLabel.formatter(800)).toBe('800 gram')
  })

  it('writes every amount in litres once the axis reaches 1000 millilitres', () => {
    const option = optionOf({ ...FLOUR, unit: 'millilitre' })

    expect(option.yAxis.axisLabel.formatter(0)).toBe('0 litre')
  })

  it('counts pieces in pieces however large the axis grows', () => {
    const option = optionOf({ ...FLOUR, unit: 'piece', availableAmount: 5000 })

    expect(option.yAxis.axisLabel.formatter(5000)).toBe('5000 piece')
  })
})

describe('the time axis of the history chart', () => {
  it('places its ticks itself, every two hours of the fourteen hours it shows', () => {
    const option = optionOf()

    expect(option.xAxis.axisTick.customValues.map((tick) => new Date(tick).toISOString())).toEqual([
      '2026-07-18T12:00:00.000Z',
      '2026-07-18T14:00:00.000Z',
      '2026-07-18T16:00:00.000Z',
      '2026-07-18T18:00:00.000Z',
      '2026-07-18T20:00:00.000Z',
      '2026-07-18T22:00:00.000Z',
      '2026-07-19T00:00:00.000Z',
      '2026-07-19T02:00:00.000Z',
    ])
    expect(option.xAxis.axisLabel.customValues).toEqual(option.xAxis.axisTick.customValues)
  })

  it('writes only the time of day when it shows one day at most of a four day festival', () => {
    const option = optionOf(FLOUR, FOUR_DAY_FESTIVAL)

    expect(option.xAxis.axisLabel.formatter(AT_13_00_UTC)).toBe('timeOfDay 2026-07-18T13:00:00.000Z')
  })

  it('writes the weekday and the time when it shows up to three days', () => {
    const option = optionOf({ ...FLOUR, runsOutAtUtcSinceStart: '2026-07-20T10:00:00Z' }, FOUR_DAY_FESTIVAL)

    expect(option.xAxis.axisLabel.formatter(AT_13_00_UTC)).toBe('weekdayAndTime 2026-07-18T13:00:00.000Z')
  })

  it('writes only the date when it shows more than three days', () => {
    const option = optionOf({ ...FLOUR, runsOutAtUtcSinceStart: '2026-07-21T20:00:00Z' }, FOUR_DAY_FESTIVAL)

    expect(option.xAxis.axisLabel.formatter(AT_13_00_UTC)).toBe('date 2026-07-18T13:00:00.000Z')
  })

  it('hides a label that would overlap the next', () => {
    expect(optionOf().xAxis.axisLabel.hideOverlap).toBe(true)
  })
})

describe('the readout under the pointer', () => {
  it('snaps the pointer to the points of the used amount', () => {
    expect(optionOf().tooltip).toMatchObject({ trigger: 'axis', axisPointer: { type: 'line', snap: true } })
  })

  it('never snaps to the available line or a forecast', () => {
    const option = optionOf()

    expect(option.series.filter((line) => line.tooltip?.show === false).map((line) => line.name)).toEqual([
      'Verfügbar',
      'Vorhersage aus den letzten 2 Stunden',
      'Vorhersage seit Festbeginn',
    ])
  })

  it('names the moment of an order and the amount used once it was in', () => {
    expect(optionOf().tooltip.formatter([{ axisValue: AT_13_00_UTC }])).toBe('2026-07-18T13:00:00.000Z used 500')
  })

  it('reads the usage point nearest to the pointer, where the line and the used amount agree', () => {
    expect(optionOf().tooltip.formatter([{ axisValue: AT_14_20_UTC }])).toBe('2026-07-18T15:00:00.000Z used 1000')
  })

  it('names now and the amount used so far when the pointer is past now', () => {
    expect(optionOf().tooltip.formatter([{ axisValue: AT_22_00_UTC }])).toBe('2026-07-18T18:00:00.000Z used 1000')
  })
})
