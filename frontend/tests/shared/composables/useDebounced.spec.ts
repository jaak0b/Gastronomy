import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { useDebounced } from '../../../src/shared/composables/useDebounced'

function mountWithADebouncedCall(delayMs: number) {
  const calls: string[] = []
  let debounced: ReturnType<typeof useDebounced<string>> | null = null
  const host = mount(
    defineComponent({
      setup() {
        debounced = useDebounced((value: string) => {
          calls.push(value)
        }, delayMs)
        return () => h('div')
      },
    }),
  )
  if (debounced === null) {
    throw new Error('The debounced call did not set up.')
  }
  return { host, calls, debounced: debounced as ReturnType<typeof useDebounced<string>> }
}

describe('a call made only once the typing stops', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('makes nothing before the delay has passed', () => {
    const { calls, debounced } = mountWithADebouncedCall(300)

    debounced.callAfterTheDelay('Tisch 1')
    vi.advanceTimersByTime(299)

    expect(calls).toEqual([])
  })

  it('makes the call with the value once the delay has passed', () => {
    const { calls, debounced } = mountWithADebouncedCall(300)

    debounced.callAfterTheDelay('Tisch 1')
    vi.advanceTimersByTime(300)

    expect(calls).toEqual(['Tisch 1'])
  })

  it('makes only the last call when the value changes within the delay', () => {
    const { calls, debounced } = mountWithADebouncedCall(300)

    debounced.callAfterTheDelay('Tisch')
    vi.advanceTimersByTime(200)
    debounced.callAfterTheDelay('Tisch 12')
    vi.advanceTimersByTime(300)

    expect(calls).toEqual(['Tisch 12'])
  })

  it('makes no call once it was cancelled', () => {
    const { calls, debounced } = mountWithADebouncedCall(300)

    debounced.callAfterTheDelay('Tisch 1')
    debounced.cancel()
    vi.advanceTimersByTime(300)

    expect(calls).toEqual([])
  })

  it('makes no call once the screen that waited for it is gone', () => {
    const { host, calls, debounced } = mountWithADebouncedCall(300)

    debounced.callAfterTheDelay('Tisch 1')
    host.unmount()
    vi.advanceTimersByTime(300)

    expect(calls).toEqual([])
  })
})
