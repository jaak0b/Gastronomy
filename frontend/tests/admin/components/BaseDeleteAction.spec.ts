import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { VDefaultsProvider } from 'vuetify/components'
import BaseDeleteAction from '../../../src/admin/components/BaseDeleteAction.vue'
import { ADMIN_CHIP_BUTTON_DEFAULTS, ADMIN_DEFAULTS } from '../../../src/admin/styles/adminSizes'
import { testPlugins } from '../../support/plugins'
import {
  dialogText,
  onScreen,
  openDialog,
  pressInDialog,
  pressTheKeyOn,
  refusalReasonsOnScreen,
  waitForDialog,
} from '../../support/dom'

const TRASH = '[data-test="delete-kiste"]'

interface Listeners {
  onAsked: () => void
  onConfirmed: () => void
  onClickAround: () => void
  onKeydownAround: (event: KeyboardEvent) => void
}

function listeners(): Listeners {
  return { onAsked: vi.fn(), onConfirmed: vi.fn(), onClickAround: vi.fn(), onKeydownAround: vi.fn() }
}

function mountInsideAClickableChip(heard: Listeners, refusalReason: string | null = null) {
  const chipWithTheTrash = defineComponent({
    render: () =>
      h(VDefaultsProvider, { defaults: ADMIN_DEFAULTS }, () =>
        h(VDefaultsProvider, { defaults: ADMIN_CHIP_BUTTON_DEFAULTS }, () =>
          h('div', { onClick: heard.onClickAround, onKeydown: heard.onKeydownAround }, [
            h(BaseDeleteAction, {
              refusalReason,
              trashTestName: 'delete-kiste',
              confirmTitle: 'Einheit löschen?',
              confirmBody: 'Die Einheit Kiste wird endgültig gelöscht.',
              confirmLabel: 'Einheit löschen',
              onAsked: heard.onAsked,
              onConfirmed: heard.onConfirmed,
            }),
          ]),
        ),
      ),
  })
  return mount(chipWithTheTrash, { global: { plugins: testPlugins('de') }, attachTo: document.body })
}

beforeEach(() => {
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('the trash that deletes after asking', () => {
  it('asks with the words it is given and says it asked, but deletes nothing yet', async () => {
    const heard = listeners()
    mountInsideAClickableChip(heard)

    onScreen(TRASH).click()

    await waitForDialog()
    expect(dialogText('[data-test="confirm-title"]')).toBe('Einheit löschen?')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe('Die Einheit Kiste wird endgültig gelöscht.')
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Einheit löschen')
    expect(heard.onAsked).toHaveBeenCalledTimes(1)
    expect(heard.onConfirmed).not.toHaveBeenCalled()
  })

  it('says the delete was confirmed once the admin confirms, and closes the question', async () => {
    const heard = listeners()
    mountInsideAClickableChip(heard)
    onScreen(TRASH).click()

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() => expect(openDialog()).toBeNull())
    expect(heard.onConfirmed).toHaveBeenCalledTimes(1)
  })

  it('confirms nothing when the admin cancels the question', async () => {
    const heard = listeners()
    mountInsideAClickableChip(heard)
    onScreen(TRASH).click()

    await pressInDialog('[data-test="cancel"]')

    await vi.waitFor(() => expect(openDialog()).toBeNull())
    expect(heard.onConfirmed).not.toHaveBeenCalled()
  })

  it('is disabled and names the reason the laptop would refuse', async () => {
    mountInsideAClickableChip(listeners(), 'Die Einheit wird noch bei den Zutaten Bier verwendet.')

    onScreen('[data-test="delete-kiste-wrapper"]').dispatchEvent(new MouseEvent('mouseenter'))

    expect((onScreen(TRASH) as HTMLButtonElement).disabled).toBe(true)
    await vi.waitFor(() =>
      expect(refusalReasonsOnScreen()).toContain('Die Einheit wird noch bei den Zutaten Bier verwendet.'),
    )
  })

  it('keeps the admin sizes on the buttons of its question, even inside a chip with small buttons', async () => {
    mountInsideAClickableChip(listeners())

    onScreen(TRASH).click()

    await waitForDialog()
    expect(onScreen(TRASH).classList.contains('v-btn--size-x-small')).toBe(true)
    expect(onScreen('[data-test="confirm"]').classList.contains('v-btn--size-default')).toBe(true)
  })
})

describe('the trash inside a clickable element', () => {
  it('keeps a tap to itself', async () => {
    const heard = listeners()
    mountInsideAClickableChip(heard)

    onScreen(TRASH).click()

    await waitForDialog()
    expect(heard.onClickAround).not.toHaveBeenCalled()
  })

  it.each([
    ['Enter', 'Enter'],
    ['Space', ' '],
  ] as const)('asks after %s and keeps the key and its click to itself', async (_name, key) => {
    const heard = listeners()
    mountInsideAClickableChip(heard)

    await pressTheKeyOn(TRASH, key)

    await waitForDialog()
    expect(heard.onKeydownAround).not.toHaveBeenCalled()
    expect(heard.onClickAround).not.toHaveBeenCalled()
  })

  it('lets every other key through', () => {
    const heard = listeners()
    mountInsideAClickableChip(heard)

    onScreen(TRASH).dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }))

    expect(heard.onKeydownAround).toHaveBeenCalledTimes(1)
  })
})
