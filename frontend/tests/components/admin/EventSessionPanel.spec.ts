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

const RUNNING_PRACTICE = JSON.stringify({
  id: 'session-2',
  name: 'Übung',
  isPractice: true,
  startedAtUtc: '2026-08-27T16:00:00Z',
  blockingConditions: [],
  requiresConfirmedName: false,
})

function refuseStartWith(conditions: unknown[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        return new Response(
          JSON.stringify({
            code: 'Conflict',
            messageKey: 'admin.event.blocked',
            parameters: {},
            details: null,
            blockingConditions: conditions,
          }),
          { status: 409 },
        )
      }
      return new Response(JSON.stringify({}), { status: 200 })
    }),
  )
}

async function typeNameAndStart(panel: ReturnType<typeof mountPanel>, selector: string) {
  await panel.get('.event-name input').setValue('Sommerfest')
  await panel.get(selector).trigger('click')
  await vi.waitFor(() => expect(panel.find('.blocking, .current').exists()).toBe(true))
}

describe('a new event the laptop refuses to start', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('says both reasons rather than showing nothing at all', async () => {
    refuseStartWith([
      { guard: 'OpenTickets', messageKey: 'admin.event.blockedOpenTickets', parameters: { count: 4 } },
      { guard: 'OpenQuestions', messageKey: 'admin.event.blockedQuestions', parameters: { count: 2 } },
    ])

    const panel = mountPanel()
    await typeNameAndStart(panel, '.start')

    expect(panel.findAll('.blocking')).toHaveLength(2)
  })

  it('renders each reason in its own words', async () => {
    refuseStartWith([
      { guard: 'OpenTickets', messageKey: 'admin.event.blockedOpenTickets', parameters: { count: 4 } },
      { guard: 'OpenQuestions', messageKey: 'admin.event.blockedQuestions', parameters: { count: 2 } },
    ])

    const panel = mountPanel()
    await typeNameAndStart(panel, '.start')

    const shown = panel.findAll('.blocking').map((row) => row.text())
    expect(shown[0]).toBe(
      'Klären Sie zuerst 4 offene Bons in der Bestellliste. Beim Start einer neuen Veranstaltung verschwinden sie von allen Telefonen.',
    )
    expect(shown[1]).toBe(
      'Beantworten Sie zuerst 2 offene Fragen zu Bons in der Bestellliste.',
    )
  })

  it('names the stations still on the test printer when that is the reason', async () => {
    refuseStartWith([
      { guard: 'MockTransport', messageKey: 'admin.event.blockedMock', parameters: { names: 'Küche' } },
    ])

    const panel = mountPanel()
    await typeNameAndStart(panel, '.start')

    expect(panel.get('.blocking').text()).toBe(
      'Tragen Sie bei Küche einen Drucker ein oder starten Sie stattdessen eine Übung. Auf dem Testdrucker kommt kein Bon auf den Stapel.',
    )
  })

  it('leaves the start button tappable so the admin can try again once it is settled', async () => {
    refuseStartWith([
      { guard: 'OpenTickets', messageKey: 'admin.event.blockedOpenTickets', parameters: { count: 4 } },
    ])

    const panel = mountPanel()
    await typeNameAndStart(panel, '.start')

    expect(panel.get('.start').attributes('disabled')).toBeUndefined()
  })
})

describe('the practice run, which the guard lets through', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    let started = false
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (init?.method === 'POST') {
          started = true
          return new Response(RUNNING_PRACTICE, { status: 201 })
        }
        return new Response(started ? RUNNING_PRACTICE : JSON.stringify({}), { status: 200 })
      }),
    )
  })

  it('starts and shows the running practice run afterwards', async () => {
    const panel = mountPanel()
    await typeNameAndStart(panel, '.practice')

    expect(panel.get('.current').text()).toContain('Übung')
  })

  it('says a practice run is going on', async () => {
    const panel = mountPanel()
    await typeNameAndStart(panel, '.practice')

    expect(panel.get('.practice-running').text()).toBe(
      'Es läuft eine Übung. Starten Sie die richtige Veranstaltung, bevor die Gäste kommen.',
    )
  })

  it('lists no blocking reason when nothing blocked it', async () => {
    const panel = mountPanel()
    await typeNameAndStart(panel, '.practice')

    expect(panel.findAll('.blocking')).toHaveLength(0)
  })
})
