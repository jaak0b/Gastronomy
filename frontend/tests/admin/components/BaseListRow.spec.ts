import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import BaseListRow from '../../../src/admin/components/BaseListRow.vue'
import { testPlugins } from '../../support/plugins'

enableAutoUnmount(afterEach)

function mountRow(refusalText: string | null = null) {
  return mount(BaseListRow, {
    props: { position: 0, name: 'Küche', nameTestName: 'station-name', refusalText },
    slots: {
      facts: '<span data-test="fact">Deaktiviert</span>',
      actions: '<button data-test="action">Bearbeiten</button>',
      default: '<p data-test="below">Beginn: 22.10.2026</p>',
    },
    global: { plugins: testPlugins() },
  })
}

describe('one row of an admin list', () => {
  it('shows the name under the test name its list gives it', () => {
    const row = mountRow()

    expect(row.get('[data-test="station-name"]').text()).toBe('Küche')
  })

  it('keeps the name, the facts and the actions on one line in that order', () => {
    const row = mountRow()

    const parts = row
      .get('[data-test="row-line"]')
      .findAll('[data-test="station-name"], [data-test="fact"], [data-test="action"]')
    expect(parts.map((part) => part.attributes('data-test'))).toEqual(['station-name', 'fact', 'action'])
  })

  it('shows what belongs below the row under its line', () => {
    const row = mountRow()

    expect(row.get('[data-test="row-line"]').find('[data-test="below"]').exists()).toBe(false)
    expect(row.get('[data-test="below"]').text()).toBe('Beginn: 22.10.2026')
  })

  it('shows the refusal of its last action under the line', () => {
    const row = mountRow('Die Ausgabestelle hat noch offene Positionen.')

    expect(row.get('[data-test="refusal"]').text()).toBe('Die Ausgabestelle hat noch offene Positionen.')
    expect(row.get('[data-test="row-line"]').find('[data-test="refusal"]').exists()).toBe(false)
  })

  it('shows no refusal while its last action was accepted', () => {
    const row = mountRow(null)

    expect(row.find('[data-test="refusal"]').exists()).toBe(false)
  })
})
