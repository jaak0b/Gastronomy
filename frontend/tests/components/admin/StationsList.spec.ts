import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import StationsList from '../../../src/components/admin/stations/StationsList.vue'
import { useConnectionStore } from '../../../src/stores/connection'
import { pressInDialog, testPlugins, waitForDialog } from '../../support/plugins'

const STATION_ID = '11111111-1111-1111-1111-111111111111'

const ONE_STATION = JSON.stringify({
  stations: [
    {
      stationId: STATION_ID,
      name: 'Küche',
      sortOrder: 1,
      isActive: true,
      hasDevice: true,
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

  it('says that the orders already placed are kept', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(ONE_STATION, { status: 200 })))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(document.querySelector('.confirm-body')!.textContent).toContain('bleiben gespeichert')
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
    await list.get('.station-form input').setValue('Küche hinten')
    await list.get('.station-form').trigger('submit')
    await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))
  }

  it('says why the new name was not taken', async () => {
    refuseTheRename()

    const list = mountList()
    await renameFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Geben Sie der Ausgabestelle einen Namen, bevor Sie sie speichern.',
    )
  })

  it('keeps the name the admin typed on screen, so it is not typed again', async () => {
    refuseTheRename()

    const list = mountList()
    await renameFirstStation(list)

    expect((list.get('.station-form input').element as HTMLInputElement).value).toBe('Küche hinten')
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
        'Diese Ausgabestelle hat noch unfertige Bestellungen und kann jetzt nicht abgeschaltet werden. Arbeiten Sie die Bestellungen ab und versuchen Sie es danach erneut.',
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
      'Ordnen Sie 2 Artikeln zuerst eine andere Ausgabestelle zu oder nehmen Sie sie von der Karte. Sonst bleiben sie ohne Ausgabestelle und können nicht bestellt werden.',
    )
  })

  it('never blames unfinished orders for an orphaned-items refusal', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).not.toContain('unfertige Bestellungen')
  })

  it('takes the singular form when a single item would be left behind', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Ordnen Sie 1 Artikel zuerst einer anderen Ausgabestelle zu oder nehmen Sie ihn von der Karte. Sonst bleibt er ohne Ausgabestelle und kann nicht bestellt werden.',
    )
  })

  it('takes the plural form when the laptop wrote the number as text', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: '2' })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Ordnen Sie 2 Artikeln zuerst eine andere Ausgabestelle zu oder nehmen Sie sie von der Karte. Sonst bleiben sie ohne Ausgabestelle und können nicht bestellt werden.',
    )
  })

  it('speaks of a single unfinished order when the laptop counted one', async () => {
    refuseDeactivationWith('admin.stationHasUnfinishedItems', { count: '1' })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Diese Ausgabestelle hat noch eine unfertige Bestellung und kann jetzt nicht abgeschaltet werden. Arbeiten Sie sie ab und versuchen Sie es danach erneut.',
    )
  })

  it('speaks of several unfinished orders when the laptop counted more than one', async () => {
    refuseDeactivationWith('admin.stationHasUnfinishedItems', { count: '2' })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Diese Ausgabestelle hat noch unfertige Bestellungen und kann jetzt nicht abgeschaltet werden. Arbeiten Sie die Bestellungen ab und versuchen Sie es danach erneut.',
    )
  })

  it('says unfinished orders when unfinished orders really are the reason', async () => {
    refuseDeactivationWith('admin.stationHasUnfinishedItems', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Diese Ausgabestelle hat noch unfertige Bestellungen und kann jetzt nicht abgeschaltet werden. Arbeiten Sie die Bestellungen ab und versuchen Sie es danach erneut.',
    )
  })

  it('falls back to a general message for a reason this app does not know', async () => {
    refuseDeactivationWith('admin.somethingAddedLater', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Das hat nicht geklappt. Versuchen Sie es noch einmal, und laden Sie die Seite neu, wenn es wieder nicht klappt.',
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

    expect(list.get('.station-name-field input').attributes('maxlength')).toBe('40')
  })
})
