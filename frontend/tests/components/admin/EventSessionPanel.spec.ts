import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import EventSessionPanel from '../../../src/components/admin/event/EventSessionPanel.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function respondWith(body: string, status = 200) {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(body, { status })))
}

function mountPanel() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(EventSessionPanel, { global: { plugins: [i18n] } })
}

const RUNNING = JSON.stringify({
  id: 'session-1',
  name: 'Sommerfest',
  isPractice: false,
  startedAtUtc: '2026-08-27T17:00:00Z',
  blockingConditions: [],
  requiresConfirmedName: false,
})

describe('the event page with no event running', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('says no event is running instead of a sentence with holes in it', async () => {
    respondWith(JSON.stringify({}))

    const panel = mountPanel()
    await vi.waitFor(() => expect(panel.find('.no-session').exists()).toBe(true))

    expect(panel.get('.no-session').text()).toBe('Keine Veranstaltung aktiv.')
  })

  it('never renders the running-event line with an empty name and time', async () => {
    respondWith(JSON.stringify({}))

    const panel = mountPanel()
    await vi.waitFor(() => expect(panel.find('.no-session').exists()).toBe(true))

    expect(panel.find('.current').exists()).toBe(false)
  })

  it('says what starting an event does', async () => {
    respondWith(JSON.stringify({}))

    const panel = mountPanel()
    await vi.waitFor(() => expect(panel.find('.no-session').exists()).toBe(true))

    expect(panel.get('.start-effect').text()).toBe(
      'Die Bonnummern beginnen wieder bei 1. Alle bisherigen Bestellungen bleiben gespeichert.',
    )
  })
})

describe('the event page with an event running', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('names the running event and when it started', async () => {
    respondWith(RUNNING)

    const panel = mountPanel()
    await vi.waitFor(() => expect(panel.find('.current').exists()).toBe(true))

    expect(panel.get('.current').text()).toContain('Sommerfest')
  })

  it('leaves the no-event line off', async () => {
    respondWith(RUNNING)

    const panel = mountPanel()
    await vi.waitFor(() => expect(panel.find('.current').exists()).toBe(true))

    expect(panel.find('.no-session').exists()).toBe(false)
  })

  it('says a practice run is going on only when it is one', async () => {
    respondWith(RUNNING)

    const panel = mountPanel()
    await vi.waitFor(() => expect(panel.find('.current').exists()).toBe(true))

    expect(panel.find('.practice-running').exists()).toBe(false)
  })
})

describe('the two ways to start something', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    respondWith(JSON.stringify({}))
  })

  it('labels the name field', () => {
    const panel = mountPanel()

    expect(panel.get('.event-name span').text()).toBe('Name der Veranstaltung')
  })

  it('offers starting a real event and starting a practice run as two separate buttons', () => {
    const panel = mountPanel()

    expect(panel.get('.start').text()).toBe('Neue Veranstaltung starten')
    expect(panel.get('.practice').text()).toBe('Übung starten')
  })

  it('puts the practice explanation with the practice button', () => {
    const panel = mountPanel()

    expect(panel.get('.practice-help').text()).toBe(
      'In einer Übung ist der Testdrucker in Ordnung, und die Bestellungen stehen später nicht in der Abrechnung.',
    )
  })

  it('refuses to start anything before the event has a name', () => {
    const panel = mountPanel()

    expect(panel.get('.start').attributes('disabled')).toBeDefined()
    expect(panel.get('.practice').attributes('disabled')).toBeDefined()
  })

  it('allows starting once a name has been typed', async () => {
    const panel = mountPanel()

    await panel.get('.event-name input').setValue('Sommerfest')

    expect(panel.get('.start').attributes('disabled')).toBeUndefined()
  })
})
