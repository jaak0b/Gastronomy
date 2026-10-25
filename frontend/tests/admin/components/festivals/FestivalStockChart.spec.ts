import { describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import FestivalStockChart from '../../../../src/admin/components/festivals/FestivalStockChart.vue'
import BaseChart from '../../../../src/admin/components/BaseChart.vue'
import type { StockChartOption } from '../../../../src/admin/core/stockChartOption'
import type { AdminFestivalIngredientView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { aFestival, anAdminFestivalIngredient } from '../../../support/wireViews'

const AT_13_00_UTC = 1_784_379_600_000

const SUMMER_FESTIVAL = aFestival({
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
  isRunning: true,
})

const FLOUR = anAdminFestivalIngredient({
  unitId: 'unit-gram',
  unitName: 'g',
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

function mountChart(ingredient: AdminFestivalIngredientView = FLOUR, locale: 'de' | 'en' = 'de'): VueWrapper {
  return mount(FestivalStockChart, {
    props: { ingredient, festival: SUMMER_FESTIVAL },
    global: { plugins: testPlugins(locale), stubs: { BaseChart: true } },
  })
}

function optionHandedToTheChart(chart: VueWrapper): StockChartOption {
  return chart.getComponent(BaseChart).props('option') as StockChartOption
}

describe('the history chart of an ingredient', () => {
  it('names the used amount and the two forecasts in the legend in German', () => {
    const chart = mountChart()

    expect(optionHandedToTheChart(chart).legend.data).toEqual([
      'Verbraucht',
      'Vorhersage aus den letzten 2 Stunden',
      'Vorhersage seit Festbeginn',
    ])
  })

  it('names the used amount and the two forecasts in the legend in English', () => {
    const chart = mountChart(FLOUR, 'en')

    expect(optionHandedToTheChart(chart).legend.data).toEqual([
      'Used',
      'Forecast from the last 2 hours',
      'Forecast since the festival started',
    ])
  })

  it('writes a moment past the right edge as an arrow and the weekday and time in German', () => {
    const chart = mountChart()

    const sinceStart = optionHandedToTheChart(chart).series.find(
      (line) => line.name === 'Vorhersage seit Festbeginn',
    )

    expect(sinceStart?.markPoint).toMatchObject({ label: { formatter: '→ So., 12:00' } })
  })

  it('writes a moment past the right edge as an arrow and the weekday and time in English', () => {
    const chart = mountChart(FLOUR, 'en')

    const sinceStart = optionHandedToTheChart(chart).series.find(
      (line) => line.name === 'Forecast since the festival started',
    )

    expect(sinceStart?.markPoint).toMatchObject({ label: { formatter: '→ Sun 12:00 PM' } })
  })

  it('labels the available line at its end in German', () => {
    const chart = mountChart()

    const available = optionHandedToTheChart(chart).series.find((line) => line.name === 'Verfügbar')

    expect(available?.markLine).toMatchObject({ label: { formatter: 'Verfügbar' } })
  })

  it('labels the available line at its end in English', () => {
    const chart = mountChart(FLOUR, 'en')

    const available = optionHandedToTheChart(chart).series.find((line) => line.name === 'Available')

    expect(available?.markLine).toMatchObject({ label: { formatter: 'Available' } })
  })

  it('draws the used amount in the data colour of the light theme', () => {
    const chart = mountChart()

    const used = optionHandedToTheChart(chart).series.find((line) => line.name === 'Verbraucht')

    expect(used).toMatchObject({ lineStyle: { color: '#0d9488' }, areaStyle: { color: '#0d9488' } })
  })

  it('names only the used amount in the legend for an ingredient without an amount', () => {
    const chart = mountChart({
      ...FLOUR,
      availableAmount: null,
      runsOutAtUtcFromLastTwoHours: null,
      runsOutAtUtcSinceStart: null,
    })

    expect(optionHandedToTheChart(chart).legend.data).toEqual(['Verbraucht'])
  })

  it('labels now in German', () => {
    const chart = mountChart()

    const used = optionHandedToTheChart(chart).series.find((line) => line.name === 'Verbraucht')

    expect(used?.markLine).toMatchObject({ label: { formatter: 'Jetzt' } })
  })

  it('names the moment the forecast reaches the available amount with its weekday for a festival past midnight', () => {
    const chart = mountChart()

    const forecast = optionHandedToTheChart(chart).series.find(
      (line) => line.name === 'Vorhersage aus den letzten 2 Stunden',
    )

    expect(forecast?.markPoint).toMatchObject({ label: { formatter: 'So., 00:00' } })
  })

  it('labels the time axis with the time of day when the chart shows one day at most', () => {
    const chart = mountChart()

    expect(optionHandedToTheChart(chart).xAxis.axisLabel.formatter(AT_13_00_UTC)).toBe('15:00')
  })

  it('labels the amount axis the way the stock section writes amounts', () => {
    const chart = mountChart()

    expect(optionHandedToTheChart(chart).yAxis.axisLabel.formatter(1500)).toBe('1.500 g')
  })

  it('labels every amount on the axis in the unit the ingredient carries', () => {
    const chart = mountChart()

    expect(optionHandedToTheChart(chart).yAxis.axisLabel.formatter(500)).toBe('500 g')
  })

  it('reads the moment of an order and the amount used once it was in, in German', () => {
    const chart = mountChart()

    expect(optionHandedToTheChart(chart).tooltip.formatter([{ axisValue: AT_13_00_UTC }])).toBe(
      'Bis Sa., 15:00 verbraucht: 500 g',
    )
  })

  it('reads the moment of an order and the amount used once it was in, in English', () => {
    const chart = mountChart(FLOUR, 'en')

    expect(optionHandedToTheChart(chart).tooltip.formatter([{ axisValue: AT_13_00_UTC }])).toBe(
      'Used by Sat 03:00 PM: 500 g',
    )
  })
})
