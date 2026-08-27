import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import LocationsList from '../../../src/components/admin/locations/LocationsList.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const ONE_STATION = JSON.stringify({
  locations: [{ id: 'location-kueche', name: 'Küche', sortOrder: 1, slipLanguage: 'de', isActive: true }],
})

function refuseDeactivationWith(messageKey: string, parameters: Record<string, unknown>) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        return new Response(
          JSON.stringify({ code: 'Conflict', messageKey, parameters, details: null }),
          { status: 409 },
        )
      }
      return new Response(ONE_STATION, { status: 200 })
    }),
  )
}

function mountList() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(LocationsList, { global: { plugins: [i18n] } })
}

async function deactivateFirstStation(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
  await list.get('li button:nth-of-type(2)').trigger('click')
  await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))
}

describe('a station the laptop refuses to switch off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('says items would be left with no station when that is the reason', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Ordnen Sie 2 Artikeln zuerst eine andere Station zu oder nehmen Sie sie von der Karte. Sonst bleiben sie ohne Station und können nicht bestellt werden.',
    )
  })

  it('never blames open slips for an orphaned-items refusal', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).not.toContain('offene Bons')
  })

  it('takes the singular form when a single item would be left behind', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Ordnen Sie 1 Artikel zuerst einer anderen Station zu oder nehmen Sie ihn von der Karte. Sonst bleibt er ohne Station und kann nicht bestellt werden.',
    )
  })

  it('says open slips when open slips really are the reason', async () => {
    refuseDeactivationWith('admin.locations.openTickets', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Diese Station hat noch 3 offene Bons und kann jetzt nicht abgeschaltet werden.',
    )
  })

  it('falls back to a general message for a reason this app does not know', async () => {
    refuseDeactivationWith('admin.somethingAddedLater', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Laden Sie die Seite neu. Die Daten konnten nicht geladen werden.',
    )
  })
})
