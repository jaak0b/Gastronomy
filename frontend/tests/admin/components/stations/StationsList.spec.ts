import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import FestivalPage from '../../../../src/admin/components/festivals/FestivalPage.vue'
import StationsList from '../../../../src/admin/components/stations/StationsList.vue'
import { useConnectionStore } from '../../../../src/shared/stores/connection'
import { pressInDialog, testPlugins, waitForDialog } from '../../../support/plugins'

const STATION_ID = '11111111-1111-1111-1111-111111111111'

const ONE_STATION = JSON.stringify({
  stations: [
    {
      stationId: STATION_ID,
      name: 'Küche',
      sortOrder: 1,
      isActive: true,
      hasDevice: true,
      isAtTheFestival: true,
    },
  ],
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
  return mount(StationsList, { global: { plugins: testPlugins() }, attachTo: document.body })
}


async function deactivateFirstStation(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
  await list.get('.deactivate').trigger('click')
  await pressInDialog('.confirm')
  await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))
}

const ONE_STATION_SWITCHED_OFF = JSON.stringify({
  stations: [{ ...JSON.parse(ONE_STATION).stations[0], isActive: false }],
})

describe('switching a station off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('asks before it happens, rather than acting on the first tap', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(urls.some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('does nothing when the question is answered with no', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.cancel')

    expect(urls.some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('switches the station off once the question is answered with yes', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/stations/${STATION_ID}/deactivate`),
    )
  })
})

describe('a new station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('is shown in the list right away even when the answer to the list is still the old one', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) => {
        if ((init?.method ?? 'GET') === 'POST') {
          return new Response(
            JSON.stringify({
              stationId: 'station-neu',
              name: 'Zelt',
              sortOrder: 2,
              isActive: true,
              hasDevice: false,
              isAtTheFestival: false,
            }),
            { status: 201 },
          )
        }
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.new-station').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.station-name-field')).not.toBeNull())
    const input = document.querySelector('.station-name-field input') as HTMLInputElement
    input.value = 'Zelt'
    input.dispatchEvent(new Event('input'))
    await list.vm.$nextTick()
    ;(document.querySelector('.form-save') as HTMLElement).click()

    await vi.waitFor(() => expect(document.querySelector('.form-dialog')).toBeNull())
    expect(list.findAll('.station-row .name').map((row) => row.text())).toContain('Zelt')
  })
})

describe('a station that is switched off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.empty').exists()).toBe(false))

    expect(list.find('.station-row').exists()).toBe(false)
  })

  it('says so on its row once it is shown', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.empty').exists()).toBe(false))
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))

    expect(list.get('.deactivated').text()).toBe('Deaktiviert')
  })

  it('offers to switch it back on without asking a question first', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.empty').exists()).toBe(false))
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))

    expect(list.get('.reactivate').text()).toBe('Ausgabestelle einschalten')
  })

  it('switches it back on at its own address', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.empty').exists()).toBe(false))
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.reactivate').trigger('click')

    await vi.waitFor(() => expect(urls).toContain(`/api/admin/stations/${STATION_ID}/activate`))
  })
})

describe('the buttons beside a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('switch that station off at its own address', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/stations/${STATION_ID}/deactivate`),
    )
  })

})

describe('setting up the tablet of a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  function stubInvitation() {
    const bodies: unknown[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (url.includes('/qr.svg')) {
          return new Response('<svg></svg>', {
            status: 200,
            headers: { 'Content-Type': 'image/svg+xml' },
          })
        }
        if (url.endsWith('/invitations')) {
          bodies.push(JSON.parse(String(init?.body ?? 'null')))
          return new Response(
            JSON.stringify({
              invitationId: 'invitation-1',
              qrUrl: 'http://192.168.0.22:5000/j/CODE',
              expiresAtUtc: '2026-08-27T20:00:00Z',
              staffMember: null,
              station: { id: STATION_ID, name: 'Küche' },
            }),
            { status: 201 },
          )
        }
        return new Response(ONE_STATION, { status: 200 })
      }),
    )
    return bodies
  }

  it('asks the laptop for a code that belongs to that station', async () => {
    const bodies = stubInvitation()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.set-up-device').trigger('click')

    await vi.waitFor(() => expect(bodies).toEqual([{ stationId: STATION_ID }]))
  })

  it('shows the same invitation panel the waiter list uses, inside the station row', async () => {
    stubInvitation()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.set-up-device').trigger('click')

    await vi.waitFor(() =>
      expect(list.get('.station-row').find('.invitation-panel').exists()).toBe(true),
    )
  })

  it('tells the admin to scan the code with the tablet of that station', async () => {
    stubInvitation()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.set-up-device').trigger('click')

    await vi.waitFor(() => expect(list.find('.instruction').exists()).toBe(true))
    expect(list.get('.instruction').text()).toBe(
      'Scannen Sie diesen QR-Code mit der Kamera des Tablets an der Ausgabestelle Küche.',
    )
  })
})

describe('a rename the laptop refuses', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  function refuseTheRename() {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          return new Response(
            JSON.stringify({
              code: 'ValidationFailed',
              messageKey: 'admin.stationNameMissing',
              parameters: {},
              details: null,
            }),
            { status: 400 },
          )
        }
        return new Response(ONE_STATION, { status: 200 })
      }),
    )
  }

  async function renameFirstStation(list: ReturnType<typeof mountList>) {
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.edit').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.station-name-field')).not.toBeNull())
    const input = document.querySelector('.station-name-field input') as HTMLInputElement
    input.value = 'Küche hinten'
    input.dispatchEvent(new Event('input'))
    await list.vm.$nextTick()
    ;(document.querySelector('.form-save') as HTMLElement).click()
    await vi.waitFor(() => expect(document.querySelector('.form-dialog .refusal')).not.toBeNull())
  }

  it('says why the new name was not taken', async () => {
    refuseTheRename()

    const list = mountList()
    await renameFirstStation(list)

    expect(document.querySelector('.form-dialog .refusal')?.textContent?.trim()).toBe(
      'Geben Sie der Ausgabestelle einen Namen, bevor Sie sie speichern.',
    )
  })

  it('keeps the name the admin typed on screen, so it is not typed again', async () => {
    refuseTheRename()

    const list = mountList()
    await renameFirstStation(list)

    expect((document.querySelector('.station-name-field input') as HTMLInputElement).value).toBe(
      'Küche hinten',
    )
  })
})

describe('two refusals one after the other', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('shows the newest one, not the one the admin has already read', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (url.endsWith('/invitations')) {
          return new Response(
            JSON.stringify({
              code: 'ValidationFailed',
              messageKey: 'enrolment.atMostOneOwner',
              parameters: {},
              details: null,
            }),
            { status: 400 },
          )
        }
        if (init?.method === 'POST') {
          return new Response(
            JSON.stringify({
              code: 'Conflict',
              messageKey: 'admin.stationHasUnfinishedItems',
              parameters: { count: 3 },
              details: null,
            }),
            { status: 409 },
          )
        }
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.set-up-device').trigger('click')
    await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(list.get('.refusal').text()).toBe(
        'Solange ein Fest aktiv ist, kann eine Ausgabestelle mit offenen Bestellungen nicht abgeschaltet werden.',
      ),
    )
  })
})

describe('the station screen the admin has left', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('no longer reloads the list when the laptop reports a change', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    list.unmount()
    urls.length = 0
    await useConnectionStore().refetchAll()

    expect(urls).toEqual([])
  })
})

describe('a station the laptop refuses to switch off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('says items would be left with no station when that is the reason', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      '2 Artikel hätten dann keine Ausgabestelle mehr. Ordnen Sie sie zuerst einer anderen Ausgabestelle zu oder entfernen Sie sie vom Fest.',
    )
  })

  it('never blames unfinished orders for an orphaned-items refusal', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).not.toContain('offene Bestellungen')
  })

  it('takes the singular form when a single item would be left behind', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      '1 Artikel hätte dann keine Ausgabestelle mehr. Ordnen Sie ihn zuerst einer anderen Ausgabestelle zu oder entfernen Sie ihn vom Fest.',
    )
  })

  it('takes the plural form when the laptop wrote the number as text', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: '2' })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      '2 Artikel hätten dann keine Ausgabestelle mehr. Ordnen Sie sie zuerst einer anderen Ausgabestelle zu oder entfernen Sie sie vom Fest.',
    )
  })

  it('falls back to a general message for a reason this app does not know', async () => {
    refuseDeactivationWith('admin.somethingAddedLater', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
    )
  })
})

describe('a refusal the admin has moved on from', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('is dropped once the admin opens a station to edit it', async () => {
    refuseDeactivationWith('admin.stationHasUnfinishedItems', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    await list.get('.edit').trigger('click')

    expect(list.find('.refusal').exists()).toBe(false)
  })

  it('is dropped once the admin starts a new station', async () => {
    refuseDeactivationWith('admin.stationHasUnfinishedItems', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    await list.get('.new-station').trigger('click')

    expect(list.find('.refusal').exists()).toBe(false)
  })
})

describe('the length of a station name', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('stops where the laptop stops storing it', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(ONE_STATION, { status: 200 })))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.edit').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.station-name-field')).not.toBeNull())

    expect(
      (document.querySelector('.station-name-field input') as HTMLInputElement).getAttribute(
        'maxlength',
      ),
    ).toBe('40')
  })
})

describe('a refusal the admin has walked away from', () => {
  const FESTIVAL_ID = 'fest-1'

  const FESTIVAL = {
    festivalId: FESTIVAL_ID,
    name: 'Sommerfest',
    startsAtUtc: '2026-07-18T10:00:00Z',
    endsAtUtc: '2026-07-19T02:00:00Z',
    isHidden: false,
    isRunning: true,
    stationCount: 1,
    menuItemCount: 0,
    orderCount: 0,
  }

  const STATION_AT_THE_FESTIVAL = {
    stationId: STATION_ID,
    name: 'Küche',
    sortOrder: 1,
    isActive: true,
    hasDevice: true,
    isAtTheFestival: true,
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  function stubTheLaptop(): void {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        const method = init?.method ?? 'GET'
        if (method !== 'GET') {
          return new Response(
            JSON.stringify({
              code: 'Conflict',
              messageKey: 'admin.stationHasOrdersAtTheFestival',
              parameters: { count: '3' },
              details: null,
            }),
            { status: 409 },
          )
        }
        if (url.startsWith('/api/admin/festivals')) {
          return new Response(JSON.stringify({ festivals: [FESTIVAL] }), { status: 200 })
        }
        if (url.startsWith('/api/admin/categories')) {
          return new Response(JSON.stringify({ categories: [] }), { status: 200 })
        }
        if (url.startsWith('/api/admin/items')) {
          return new Response(JSON.stringify({ items: [] }), { status: 200 })
        }
        return new Response(JSON.stringify({ stations: [STATION_AT_THE_FESTIVAL] }), {
          status: 200,
        })
      }),
    )
  }

  function mountFestivalPage() {
    return mount(FestivalPage, {
      props: { festivalId: FESTIVAL_ID },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
  }

  it('does not follow the admin from a festival page to the stations page', async () => {
    stubTheLaptop()

    const festival = mountFestivalPage()
    await vi.waitFor(() => expect(festival.find('.remove-station').exists()).toBe(true))
    await festival.get('.remove-station').trigger('click')
    await pressInDialog('.confirm')
    await vi.waitFor(() =>
      expect(festival.find('.festival-station-row .refusal').exists()).toBe(true),
    )

    festival.unmount()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))

    expect(list.find('.refusal').exists()).toBe(false)
  })

  it('does not follow the admin from the stations page to a festival page', async () => {
    stubTheLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')
    await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))

    list.unmount()
    const festival = mountFestivalPage()
    await vi.waitFor(() => expect(festival.find('.festival-station-row').exists()).toBe(true))

    expect(festival.find('.festival-stations .refusal').exists()).toBe(false)
  })

  it('does not keep a refused device setup when the admin leaves and returns', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (url.endsWith('/invitations')) {
          return new Response(
            JSON.stringify({
              code: 'Conflict',
              messageKey: 'enrolment.atMostOneOwner',
              parameters: {},
              details: null,
            }),
            { status: 409 },
          )
        }
        return new Response(JSON.stringify({ stations: [STATION_AT_THE_FESTIVAL] }), {
          status: 200,
        })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.set-up-device').trigger('click')
    await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))

    list.unmount()
    const again = mountList()
    await vi.waitFor(() => expect(again.find('.station-row').exists()).toBe(true))

    expect(again.find('.refusal').exists()).toBe(false)
  })
})
