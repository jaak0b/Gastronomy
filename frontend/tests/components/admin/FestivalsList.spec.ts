import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import FestivalsList from '../../../src/components/admin/festivals/FestivalsList.vue'
import { pressInDialog, testPlugins, waitForDialog } from '../../support/plugins'

const RUNNING = {
  festivalId: 'fest-1',
  name: 'Sommerfest',
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
  isHidden: false,
  isRunning: true,
  stationCount: 2,
  menuItemCount: 8,
  orderCount: 1,
}

const OVER = {
  ...RUNNING,
  festivalId: 'fest-2',
  name: 'Herbstfest',
  isRunning: false,
  orderCount: 0,
}

const HIDDEN = {
  ...OVER,
  festivalId: 'fest-3',
  name: 'Sommerfest 2025',
  isHidden: true,
}

interface Call {
  url: string
  method: string
  body: unknown
}

function stubLaptop(festivals: unknown[]): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calls.push({
        url,
        method: init?.method ?? 'GET',
        body: init?.body === undefined ? null : JSON.parse(String(init.body)),
      })
      return new Response(JSON.stringify({ festivals }), { status: 200 })
    }),
  )
  return calls
}

function mountList() {
  return mount(FestivalsList, { global: { plugins: testPlugins() }, attachTo: document.body })
}

describe('the list of festivals', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names the festival that is running now', async () => {
    stubLaptop([RUNNING])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.festival-row').exists()).toBe(true))

    expect(list.get('.festival-row .name').text()).toBe('Sommerfest')
    expect(list.get('.festival-row .running').text()).toBe('Aktiv')
  })

  it('counts the stations, the items and the orders of each festival', async () => {
    stubLaptop([RUNNING])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.festival-row').exists()).toBe(true))

    expect(list.get('.station-count').text()).toBe('2 Ausgabestellen')
    expect(list.get('.menu-item-count').text()).toBe('8 Artikel')
    expect(list.get('.order-count').text()).toBe('1 Bestellung')
  })

  it('leaves the hidden ones out until the admin asks to see them', async () => {
    stubLaptop([RUNNING, HIDDEN])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.festival-row').exists()).toBe(true))
    expect(list.findAll('.festival-row')).toHaveLength(1)

    await list.get('.show-hidden input').setValue(true)

    expect(list.findAll('.festival-row')).toHaveLength(2)
  })

  it('says how to start when the laptop knows no festival at all', async () => {
    stubLaptop([])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.none-yet').exists()).toBe(true))

    expect(list.get('.none-yet').text()).toBe(
      'Es ist noch kein Fest angelegt. Klicken Sie auf "Neues Fest".',
    )
  })

  it("keeps every row's buttons in one shared group so they line up", async () => {
    stubLaptop([RUNNING, OVER])

    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('.festival-row')).toHaveLength(2))

    const rows = list.findAll('.festival-row')
    expect(rows[0].find('.actions .open').exists()).toBe(true)
    expect(rows[0].find('.actions .copy').exists()).toBe(true)
    expect(rows[1].find('.actions .hide').exists()).toBe(true)

    const reserved = rows[0].get('.conditional-action .action-measure')
    expect(reserved.text()).toContain('Ausblenden')
    expect(reserved.text()).toContain('Einblenden')
  })

  it('offers no way to hide the festival that is running', async () => {
    stubLaptop([RUNNING, OVER])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.festival-row').exists()).toBe(true))

    const rows = list.findAll('.festival-row')
    expect(rows[0].find('.hide').exists()).toBe(false)
    expect(rows[1].find('.hide').exists()).toBe(true)
  })

  it('opens the page of that festival, which is where its stations and its menu live', async () => {
    stubLaptop([RUNNING])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.open').exists()).toBe(true))
    expect(list.get('.open').text()).toBe('Fest bearbeiten')

    await list.get('.open').trigger('click')

    expect(window.location.pathname).toBe('/admin/festivals/fest-1')
  })

  it('asks before a festival is hidden and hides it once the answer is yes', async () => {
    const calls = stubLaptop([OVER])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.hide').exists()).toBe(true))
    await list.get('.hide').trigger('click')
    await waitForDialog()
    expect(calls.some((call) => call.method === 'POST')).toBe(false)

    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(calls.find((call) => call.method === 'POST')?.url).toBe(
        '/api/admin/festivals/fest-2/hide',
      ),
    )
  })

  it('sends a new festival with the moments the admin typed, in UTC', async () => {
    const calls = stubLaptop([])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.new-festival').exists()).toBe(true))
    await list.get('.new-festival').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.festival-form')).not.toBeNull())

    const form = document.querySelector('.festival-form') as HTMLElement
    const fill = (selector: string, value: string): void => {
      const field = form.querySelector(selector) as HTMLInputElement
      field.value = value
      field.dispatchEvent(new Event('input'))
    }
    fill('.festival-name-field input', 'Herbstfest')
    fill('.festival-start-field input', '2026-10-03T12:00')
    fill('.festival-end-field input', '2026-10-04T15:00')
    await vi.waitFor(() =>
      expect((form.querySelector('.confirm') as HTMLButtonElement).disabled).toBe(false),
    )
    ;(form.querySelector('.confirm') as HTMLElement).click()

    await vi.waitFor(() => {
      const sent = calls.find((call) => call.method === 'POST')
      expect(sent?.url).toBe('/api/admin/festivals')
      expect(sent?.body).toEqual({
        name: 'Herbstfest',
        startsAtUtc: new Date('2026-10-03T12:00').toISOString(),
        endsAtUtc: new Date('2026-10-04T15:00').toISOString(),
      })
    })
  })

  it('copies a festival under a name and a period typed fresh', async () => {
    const calls = stubLaptop([RUNNING])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.copy').exists()).toBe(true))
    await list.get('.copy').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.festival-form')).not.toBeNull())

    const form = document.querySelector('.festival-form') as HTMLElement
    expect((form.querySelector('.festival-name-field input') as HTMLInputElement).value).toBe('')
    expect(calls.some((call) => call.method === 'POST')).toBe(false)
  })
})
