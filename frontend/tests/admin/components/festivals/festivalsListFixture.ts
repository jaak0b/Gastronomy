import { mount } from '@vue/test-utils'
import FestivalsList from '../../../../src/admin/components/festivals/FestivalsList.vue'
import { testPlugins } from '../../../support/plugins'
import { stubLaptop, answer, type StubbedLaptop } from '../../../support/laptop'

export const RUNNING = {
  festivalId: 'fest-1',
  name: 'Sommerfest',
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
  isHidden: false,
  isRunning: true,
  stationCount: 2,
  menuItemCount: 8,
  orderCount: 1,
}

export const OVER = {
  ...RUNNING,
  festivalId: 'fest-2',
  name: 'Herbstfest',
  isRunning: false,
  orderCount: 0,
}

export const HIDDEN = {
  ...OVER,
  festivalId: 'fest-3',
  name: 'Sommerfest 2025',
  isHidden: true,
}

export const RUNNING_ROW = '[data-test="festival-row"][data-test-id="fest-1"]'

export const OVER_ROW = '[data-test="festival-row"][data-test-id="fest-2"]'

export function festivalsLaptop(festivals: unknown[]): StubbedLaptop {
  return stubLaptop().answersEverythingElse(answer({ festivals }))
}

export function mountList(locale: 'de' | 'en' = 'de') {
  return mount(FestivalsList, { global: { plugins: testPlugins(locale) }, attachTo: document.body })
}
