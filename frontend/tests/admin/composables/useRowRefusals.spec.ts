import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { adminFailed, adminOk } from '../../../src/admin/core/adminActionResult'
import { adminErrorMessageForKey } from '../../../src/admin/core/adminErrorMessage'
import { useRowRefusals, type RowRefusals } from '../../../src/admin/composables/useRowRefusals'
import { testPlugins } from '../../support/plugins'

function rowRefusalsIn(locale: 'de' | 'en'): RowRefusals {
  let refusals: RowRefusals | null = null
  mount(
    defineComponent({
      setup() {
        refusals = useRowRefusals()
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins(locale) } },
  )
  if (refusals === null) {
    throw new Error('the composable was never set up')
  }
  return refusals
}

const AMOUNT_REFUSED = adminFailed(adminErrorMessageForKey('errors.admin.ingredients.amountInvalid'))

describe('the refusals under the rows of a list', () => {
  it('show the sentence of a refused action under that row and no other', () => {
    const refusals = rowRefusalsIn('de')

    refusals.showRefusalOf('ingredient-mehl', AMOUNT_REFUSED)

    expect([refusals.refusalTextOf('ingredient-mehl'), refusals.refusalTextOf('ingredient-milch')]).toEqual([
      'Geben Sie eine Menge größer als null an.',
      null,
    ])
  })

  it('write a refusal named by its key with its parameters in English on an English device', () => {
    const refusals = rowRefusalsIn('en')

    refusals.refuse('item-bratwurst', 'admin.festivals.errors.namedItemNeedsAStation', { item: 'Bratwurst' })

    expect(refusals.refusalTextOf('item-bratwurst')).toBe('Bratwurst needs at least one station.')
  })

  it('keep the refusal of a row when a later action on that row is accepted', () => {
    const refusals = rowRefusalsIn('de')
    refusals.refuse('ingredient-mehl', 'errors.admin.ingredients.amountInvalid')

    refusals.showRefusalOf('ingredient-mehl', adminOk(null))

    expect(refusals.refusalTextOf('ingredient-mehl')).toBe('Geben Sie eine Menge größer als null an.')
  })

  it('drop the refusal of a row once it is forgotten', () => {
    const refusals = rowRefusalsIn('de')
    refusals.refuse('ingredient-mehl', 'errors.admin.ingredients.amountInvalid')

    refusals.forgetRefusalOf('ingredient-mehl')

    expect([refusals.refusalTextOf('ingredient-mehl'), refusals.isRefused('ingredient-mehl')]).toEqual([null, false])
  })
})
