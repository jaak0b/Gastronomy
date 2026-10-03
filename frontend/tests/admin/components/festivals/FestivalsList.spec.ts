import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import FestivalsList from '../../../../src/admin/components/festivals/FestivalsList.vue'
import { testPlugins } from '../../../support/plugins'
import { pressInDialog, waitForDialog, typeInto, onScreen } from '../../../support/dom'
import { stubLaptop, answer, type StubbedLaptop, refusal } from '../../../support/laptop'

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

function festivalsLaptop(festivals: unknown[]): StubbedLaptop {
  return stubLaptop().answersEverythingElse(answer({ festivals }))
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
    festivalsLaptop([RUNNING])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="festival-row"]').exists()).toBe(true))

    expect(list.get('[data-test="festival-row"] [data-test="name"]').text()).toBe('Sommerfest')
    expect(list.get('[data-test="festival-row"] [data-test="running"]').text()).toBe('Aktiv')
  })

  it('counts the stations, the items and the orders of each festival', async () => {
    festivalsLaptop([RUNNING])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="festival-row"]').exists()).toBe(true))

    expect(list.get('[data-test="station-count"]').text()).toBe('2 Ausgabestellen')
    expect(list.get('[data-test="menu-item-count"]').text()).toBe('8 Artikel')
    expect(list.get('[data-test="order-count"]').text()).toBe('1 Bestellung')
  })

  it('leaves the hidden ones out until the admin asks to see them', async () => {
    festivalsLaptop([RUNNING, HIDDEN])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="festival-row"]').exists()).toBe(true))
    expect(list.findAll('[data-test="festival-row"]')).toHaveLength(1)

    await list.get('[data-test="show-hidden"] input').setValue(true)

    expect(list.findAll('[data-test="festival-row"]')).toHaveLength(2)
  })

  it('says how to start when the laptop knows no festival at all', async () => {
    festivalsLaptop([])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="none-yet"]').exists()).toBe(true))

    expect(list.get('[data-test="none-yet"]').text()).toBe(
      'Es ist noch kein Fest angelegt. Klicken Sie auf "Neues Fest".',
    )
  })

  it("keeps every row's buttons in one shared group so they line up", async () => {
    festivalsLaptop([RUNNING, OVER])

    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('[data-test="festival-row"]')).toHaveLength(2))

    const rows = list.findAll('[data-test="festival-row"]')
    expect(rows[0].find('[data-test="actions"] [data-test="open"]').exists()).toBe(true)
    expect(rows[0].find('[data-test="actions"] [data-test="copy"]').exists()).toBe(true)
    expect(rows[1].find('[data-test="actions"] [data-test="hide"]').exists()).toBe(true)

    const reserved = rows[0].get('[data-test="conditional-action"] [data-test="action-measure"]')
    expect(reserved.text()).toContain('Ausblenden')
    expect(reserved.text()).toContain('Einblenden')
  })

  it('offers no way to hide the festival that is running', async () => {
    festivalsLaptop([RUNNING, OVER])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="festival-row"]').exists()).toBe(true))

    const rows = list.findAll('[data-test="festival-row"]')
    expect(rows[0].find('[data-test="hide"]').exists()).toBe(false)
    expect(rows[1].find('[data-test="hide"]').exists()).toBe(true)
  })

  it('opens the page of that festival, which is where its stations and its menu live', async () => {
    festivalsLaptop([RUNNING])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="open"]').exists()).toBe(true))
    expect(list.get('[data-test="open"]').text()).toBe('Fest bearbeiten')

    await list.get('[data-test="open"]').trigger('click')

    expect(window.location.pathname).toBe('/admin/festivals/fest-1')
  })

  it('asks before a festival is hidden and hides it once the answer is yes', async () => {
    const laptop = festivalsLaptop([OVER])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="hide"]').exists()).toBe(true))
    await list.get('[data-test="hide"]').trigger('click')
    await waitForDialog()
    expect(laptop.calls.some((call) => call.method === 'POST')).toBe(false)

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.calls.find((call) => call.method === 'POST')?.url).toBe(
        '/api/admin/festivals/fest-2/hide',
      ),
    )
  })

  it('sends a new festival with the moments the admin typed, in UTC', async () => {
    const laptop = festivalsLaptop([])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="new-festival"]').exists()).toBe(true))
    await list.get('[data-test="new-festival"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    const form = document.querySelector('[data-test="form-dialog"]') as HTMLElement
    typeInto(form.querySelector<HTMLElement>('[data-test="festival-name-field"] input') as HTMLElement, 'Herbstfest')
    typeInto(form.querySelector<HTMLElement>('[data-test="festival-start-field"] input') as HTMLElement, '2026-10-03T12:00')
    typeInto(form.querySelector<HTMLElement>('[data-test="festival-end-field"] input') as HTMLElement, '2026-10-04T15:00')
    await vi.waitFor(() =>
      expect((form.querySelector('[data-test="form-save"]') as HTMLButtonElement).disabled).toBe(false),
    )
    ;(form.querySelector('[data-test="form-save"]') as HTMLElement).click()

    await vi.waitFor(() => {
      const sent = laptop.calls.find((call) => call.method === 'POST')
      expect(sent?.url).toBe('/api/admin/festivals')
      expect(sent?.body).toEqual({
        name: 'Herbstfest',
        startsAtUtc: new Date('2026-10-03T12:00').toISOString(),
        endsAtUtc: new Date('2026-10-04T15:00').toISOString(),
      })
    })
  })

  it('copies a festival under a name and a period typed fresh', async () => {
    const laptop = festivalsLaptop([RUNNING])

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="copy"]').exists()).toBe(true))
    await list.get('[data-test="copy"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    const form = document.querySelector('[data-test="form-dialog"]') as HTMLElement
    expect((form.querySelector('[data-test="festival-name-field"] input') as HTMLInputElement).value).toBe('')
    expect(laptop.calls.some((call) => call.method === 'POST')).toBe(false)
  })

  it('shows a refusal inside the dialog, not behind it as well', async () => {
    festivalsLaptop([]).answers(
      'POST',
      /./,
      refusal('errors.admin.actionFailed', { status: 400, code: 'ValidationFailed' }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="new-festival"]').exists()).toBe(true))
    await list.get('[data-test="new-festival"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    const form = document.querySelector('[data-test="form-dialog"]') as HTMLElement
    typeInto(onScreen('[data-test="festival-name-field"] input', form), 'Herbstfest')
    typeInto(onScreen('[data-test="festival-start-field"] input', form), '2026-10-03T12:00')
    typeInto(onScreen('[data-test="festival-end-field"] input', form), '2026-10-04T15:00')
    await vi.waitFor(() =>
      expect((form.querySelector('[data-test="form-save"]') as HTMLButtonElement).disabled).toBe(false),
    )
    ;(form.querySelector('[data-test="form-save"]') as HTMLElement).click()

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"] [data-test="refusal"]')).not.toBeNull())
    expect(list.find('[data-test="refusal"]').exists()).toBe(false)
  })
})
