import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { answer, noConnection, stubLaptop } from '../../../support/laptop'
import { ONE_STATION_SWITCHED_OFF, mountList } from './stationsListFixture'

describe('the text of an empty stations tab', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is the shared text of an empty list once the laptop sent no station', async () => {
    stubLaptop().answersEverythingElse(answer({ stations: [] }))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="empty-list-text"]').exists()).toBe(true))

    expect(list.get('[data-test="empty-list-text"]').text()).toBe('Keine Einträge')
  })

  it('stays away after the load failed', async () => {
    stubLaptop().answersEverythingElse(noConnection())

    const list = mountList()
    await flushPromises()

    expect(list.find('[data-test="empty-list-text"]').exists()).toBe(false)
  })

  it('stays away while every station is deactivated, because the list is not empty', async () => {
    const laptop = stubLaptop().answersEverythingElse(answer(ONE_STATION_SWITCHED_OFF))

    const list = mountList()
    await vi.waitFor(() => expect(laptop.urls()).toContain('/api/admin/stations'))
    await flushPromises()

    expect(list.find('[data-test="empty-list-text"]').exists()).toBe(false)
  })
})
