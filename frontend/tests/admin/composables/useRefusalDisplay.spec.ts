import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { adminFailed, adminOk } from '../../../src/admin/core/adminActionResult'
import { adminErrorMessageForKey } from '../../../src/admin/core/adminErrorMessage'
import { useRefusalDisplay } from '../../../src/admin/composables/useRefusalDisplay'
import { testPlugins } from '../../support/plugins'

function refusalDisplayIn(locale: 'de' | 'en'): ReturnType<typeof useRefusalDisplay> {
  let display: ReturnType<typeof useRefusalDisplay> | null = null
  mount(
    defineComponent({
      setup() {
        display = useRefusalDisplay()
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins(locale) } },
  )
  if (display === null) {
    throw new Error('the composable was never set up')
  }
  return display
}

const HAS_ACTIVE_ITEMS = adminFailed(
  adminErrorMessageForKey('errors.admin.categories.hasActiveItems'),
)

describe('the refusal a screen shows', () => {
  it('is empty until an action is refused', () => {
    const display = refusalDisplayIn('de')

    expect(display.refusalText.value).toBeNull()
  })

  it('shows the sentence of a refused action and reports that it was refused', () => {
    const display = refusalDisplayIn('de')

    const wasRefused = display.showRefusalOf(HAS_ACTIVE_ITEMS)

    expect(wasRefused).toBe(true)
    expect(display.refusalText.value).toBe(
      'Diese Kategorie hat noch eingeschaltete Artikel. Schalten Sie die Artikel ab und danach die Kategorie.',
    )
  })

  it('writes the sentence in English on an English device', () => {
    const display = refusalDisplayIn('en')

    display.showRefusalOf(HAS_ACTIVE_ITEMS)

    expect(display.refusalText.value).toBe(
      'This category still has items switched on. Switch the items off first, then the category.',
    )
  })

  it('clears an earlier refusal when the next action is accepted and reports no refusal', () => {
    const display = refusalDisplayIn('de')
    display.showRefusalOf(HAS_ACTIVE_ITEMS)

    const wasRefused = display.showRefusalOf(adminOk(null))

    expect(wasRefused).toBe(false)
    expect(display.refusalText.value).toBeNull()
  })
})
