import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BaseRefusalAlert from '../../../src/shared/components/BaseRefusalAlert.vue'

describe('the warning that names why the laptop refused an action', () => {
  it('shows the sentence it is given as a warning', () => {
    const alert = mount(BaseRefusalAlert, { props: { text: 'Die Aktion ist fehlgeschlagen.' } })

    expect(alert.get('[data-test="refusal"]').text()).toBe('Die Aktion ist fehlgeschlagen.')
    expect(alert.getComponent({ name: 'VAlert' }).props('type')).toBe('warning')
  })
})
