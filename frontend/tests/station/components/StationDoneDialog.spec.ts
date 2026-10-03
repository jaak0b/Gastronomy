import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StationDoneDialog from '../../../src/station/components/StationDoneDialog.vue'
import type { ItemLine } from '../../../src/shared/core/stationBoard'
import { testPlugins } from '../../support/plugins'
import { clickOn, textOnScreen, waitUntil } from '../../support/dom'

const TWO_BRATWURST: ItemLine = { key: 'bratwurst', itemName: 'Bratwurst', note: null, units: 2 }
const ONE_NOTED_BRATWURST: ItemLine = {
  key: 'bratwurst-ohne-senf',
  itemName: 'Bratwurst',
  note: 'ohne Senf',
  units: 1,
}

function mountTheQuestion(locale: 'de' | 'en' = 'de') {
  return mount(StationDoneDialog, {
    props: { tableName: '4', lines: [TWO_BRATWURST, ONE_NOTED_BRATWURST] },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

function unitTexts(): string[] {
  return [...document.querySelectorAll('[data-test="station-done-dialog"] [data-test="unit"]')].map(
    (unit) => unit.textContent?.trim() ?? '',
  )
}

describe('the question a station asks before items count as done', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('repeats the table the tray goes to', async () => {
    mountTheQuestion()

    await waitUntil(() => expect(unitTexts()).toHaveLength(2))

    expect(textOnScreen('[data-test="row-table"]').trim()).toBe('Tisch 4')
  })

  it('lists each article with its note on a line of its own', async () => {
    mountTheQuestion('en')

    await waitUntil(() => expect(unitTexts()).toHaveLength(2))

    expect(unitTexts()).toEqual(['2 x Bratwurst', '1 x Bratwurst · Note: ohne Senf'])
  })

  it('reports a cancel', async () => {
    const question = mountTheQuestion()
    await waitUntil(() => expect(unitTexts()).toHaveLength(2))

    await clickOn('[data-test="station-done-dialog"] [data-test="cancel"]')

    expect(question.emitted('cancelled')).toEqual([[]])
  })

  it('confirms nothing when cancelled', async () => {
    const question = mountTheQuestion()
    await waitUntil(() => expect(unitTexts()).toHaveLength(2))

    await clickOn('[data-test="station-done-dialog"] [data-test="cancel"]')

    expect(question.emitted('confirmed')).toBeUndefined()
  })
})
