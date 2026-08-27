import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import UnknownQuestion from '../../../src/components/orders/UnknownQuestion.vue'
import type { TicketSummary } from '../../../src/core/apiTypes'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function ticket(printerHasPaper: boolean | null): TicketSummary {
  return {
    ticketId: 'ticket-1',
    locationId: 'location-kueche',
    locationName: 'Küche',
    sequenceNumber: 42,
    status: 'Unknown',
    failureReason: 'SocketDropped',
    printerHasPaper,
  }
}

function mountQuestion(printerHasPaper: boolean | null, noticeKey: string | null = null) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(UnknownQuestion, {
    props: { ticket: ticket(printerHasPaper), noticeKey },
    global: { plugins: [i18n] },
  })
}

describe('UnknownQuestion', () => {
  it('sends the server to the pile with the station and the padded slip number', () => {
    const question = mountQuestion(true)

    expect(question.get('.action').text()).toBe(
      'Schauen Sie am Stapel bei Küche nach Bon 042 und antworten Sie hier.',
    )
  })

  it('says why nobody can tell from here whether the slip printed', () => {
    const question = mountQuestion(true)

    expect(question.get('.reason').text()).toBe(
      'Die Verbindung zum Drucker ist abgerissen, während der Bon gesendet wurde, deshalb ist hier nicht bekannt, ob er gedruckt wurde.',
    )
  })

  it('adds the paper hint when the printer has also run out of paper', () => {
    const question = mountQuestion(false)

    expect(question.get('.paper-hint').text()).toBe('Der Drucker hat außerdem kein Papier mehr.')
  })

  it('leaves the paper hint off when the printer still has paper', () => {
    const question = mountQuestion(true)

    expect(question.find('.paper-hint').exists()).toBe(false)
  })

  it('answers that the slip is on the pile', async () => {
    const question = mountQuestion(true)

    await question.get('.slip-is-there').trigger('click')

    expect(question.emitted('answer')).toEqual([[true]])
  })

  it('answers that the slip is missing', async () => {
    const question = mountQuestion(true)

    await question.get('.slip-is-missing').trigger('click')

    expect(question.emitted('answer')).toEqual([[false]])
  })

  it('says the question was already answered when somebody else got there first', () => {
    const question = mountQuestion(true, 'ticket.unknown.answered')

    expect(question.get('.notice').text()).toBe('Diese Frage wurde bereits beantwortet.')
  })

  it('offers no answer once the question was already answered', () => {
    const question = mountQuestion(true, 'ticket.unknown.answered')

    expect(question.find('.slip-is-there').exists()).toBe(false)
    expect(question.find('.slip-is-missing').exists()).toBe(false)
  })
})
