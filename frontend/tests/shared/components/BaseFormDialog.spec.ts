import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { h } from 'vue'
import BaseFormDialog from '../../../src/shared/components/BaseFormDialog.vue'
import { testPlugins } from '../../support/plugins'
import { isOnScreen, onScreen, waitUntil } from '../../support/dom'

async function openFormDialog(wide?: boolean): Promise<void> {
  mount(BaseFormDialog, {
    props: { title: 'Neue Zutat', errorText: null, wide },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
  await waitUntil(() => expect(isOnScreen('[data-test="form-dialog"]')).toBe(true))
}

function widthOfTheDialog(): string | undefined {
  return onScreen('[data-test="form-dialog"]').closest<HTMLElement>('.v-overlay__content')?.style.maxWidth
}

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('the width of a form dialog', () => {
  it('is the width of an ordinary form when nothing asks for more', async () => {
    await openFormDialog()

    expect(widthOfTheDialog()).toBe('560px')
  })

  it('is wider when the form asks to be wide', async () => {
    await openFormDialog(true)

    expect(widthOfTheDialog()).toBe('880px')
  })
})

describe('a form dialog that opens', () => {
  it('puts the cursor into its first field so the admin can type at once', async () => {
    mount(BaseFormDialog, {
      props: { title: 'Neue Zutat', errorText: null },
      slots: {
        default: () => [
          h('input', { 'data-test': 'first-field' }),
          h('input', { 'data-test': 'second-field' }),
        ],
      },
      global: { plugins: testPlugins(), stubs: { transition: false } },
      attachTo: document.body,
    })

    await waitUntil(() => expect(document.activeElement).toBe(onScreen('[data-test="first-field"]')))
  })
})

describe('the actions of a form dialog', () => {
  it('offers a cancel and a save that submits the form', async () => {
    await openFormDialog()

    expect({
      saveType: onScreen('[data-test="form-save"]').getAttribute('type'),
      saveDisabled: onScreen('[data-test="form-save"]').hasAttribute('disabled'),
      cancelDisabled: onScreen('[data-test="form-cancel"]').hasAttribute('disabled'),
    }).toEqual({ saveType: 'submit', saveDisabled: false, cancelDisabled: false })
  })

  it('shows only the closing action when the form only closes', async () => {
    const dialog = mount(BaseFormDialog, {
      props: { title: 'Rezept', errorText: null, closeOnly: true, cancelLabel: 'Schließen' },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await waitUntil(() => expect(isOnScreen('[data-test="form-cancel"]')).toBe(true))

    onScreen('[data-test="form-cancel"]').click()
    await flushPromises()

    expect({
      save: isOnScreen('[data-test="form-save"]'),
      label: onScreen('[data-test="form-cancel"]').textContent?.trim(),
      cancelled: dialog.emitted('cancel'),
    }).toEqual({ save: false, label: 'Schließen', cancelled: [[]] })
  })
})
