import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import BaseDialogButton from '../../../src/shared/components/BaseDialogButton.vue'
import { testPlugins } from '../../support/plugins'

enableAutoUnmount(afterEach)

type DialogButtonIntent = 'primary' | 'secondary' | 'quiet'

function mountButton(intent: DialogButtonIntent, attributes: Record<string, unknown> = {}) {
  return mount(BaseDialogButton, {
    props: { intent },
    slots: { default: 'Speichern' },
    attrs: attributes,
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

function buttonOf(wrapper: ReturnType<typeof mountButton>) {
  return wrapper.getComponent({ name: 'VBtn' })
}

describe('a dialog button', () => {
  it.each([
    { intent: 'primary', variant: 'flat', color: 'primary' },
    { intent: 'secondary', variant: 'tonal', color: 'primary' },
    { intent: 'quiet', variant: 'outlined', color: undefined },
  ] as const)('maps $intent to $variant with $color', ({ intent, variant, color }) => {
    const button = buttonOf(mountButton(intent))

    expect({ variant: button.props('variant'), color: button.props('color') }).toEqual({ variant, color })
  })

  it('uses its default slot as the label', () => {
    expect(mountButton('primary').text()).toBe('Speichern')
  })

  it('passes class, data-test, disabled and type through to the button', () => {
    const button = mountButton('primary', {
      class: 'extra',
      'data-test': 'form-save',
      disabled: true,
      type: 'submit',
    }).get('button')

    expect({
      extraClass: button.classes().includes('extra'),
      testName: button.attributes('data-test'),
      disabled: button.element.hasAttribute('disabled'),
      type: button.attributes('type'),
    }).toEqual({ extraClass: true, testName: 'form-save', disabled: true, type: 'submit' })
  })
})
