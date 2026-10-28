import { afterEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount, type VueWrapper } from '@vue/test-utils'
import BaseIconAction from '../../../src/shared/components/BaseIconAction.vue'
import { testPlugins } from '../../support/plugins'

enableAutoUnmount(afterEach)

function mountAction(location?: 'top' | 'bottom') {
  return mount(BaseIconAction, {
    props: { icon: 'mdi-eye-off', label: 'Ausblenden', location },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

function tooltipLocation(action: VueWrapper): unknown {
  return action.getComponent({ name: 'VTooltip' }).props('location')
}

describe('the icon button with its name as a tooltip', () => {
  it('shows its name when the pointer rests on it', async () => {
    const action = mountAction()

    await action.trigger('mouseenter')

    const overlay = action.getComponent({ name: 'VTooltip' }).getComponent({ name: 'VOverlay' })
    await vi.waitFor(() =>
      expect((overlay.vm as unknown as { contentEl?: HTMLElement }).contentEl?.textContent).toBe('Ausblenden'),
    )
  })

  it('places the tooltip above the button unless told otherwise', () => {
    expect(tooltipLocation(mountAction())).toBe('top')
  })

  it('places the tooltip below the button when asked to', () => {
    expect(tooltipLocation(mountAction('bottom'))).toBe('bottom')
  })
})
