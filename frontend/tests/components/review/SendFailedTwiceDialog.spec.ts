import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SendFailedTwiceDialog from '../../../src/components/review/SendFailedTwiceDialog.vue'
import { testPlugins } from '../../support/plugins'

function mountDialog(locale: 'de' | 'en' = 'de') {
  document.body.innerHTML = ''
  return mount(SendFailedTwiceDialog, {
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

function textOf(selector: string): string {
  return document.querySelector(selector)?.textContent?.trim() ?? ''
}

describe('the dialog after a second failed send, in German', () => {
  it('names what happened in its heading', () => {
    mountDialog()

    expect(textOf('.send-failed-twice-dialog .title')).toBe('Die Bestellung wurde nicht gesendet')
  })

  it('admits that nobody can tell whether the order arrived', () => {
    mountDialog()

    expect(textOf('.send-failed-twice-dialog .what-happened')).toBe(
      'Das Telefon hat den Laptop zweimal nicht erreicht. Wir wissen nicht, ob die Bestellung trotzdem angekommen ist.',
    )
  })

  it('asks for the order on a slip of paper', () => {
    mountDialog()

    expect(textOf('.send-failed-twice-dialog .write-it-down')).toBe(
      'Schreiben Sie die Bestellung auf einen Zettel.',
    )
  })

  it('says how the tablet at the station settles whether the slip is needed', () => {
    mountDialog()

    expect(textOf('.send-failed-twice-dialog .check-the-tablet')).toBe(
      'Schauen Sie an der Ausgabestelle auf das Tablet. Steht die Bestellung dort schon, werfen Sie den Zettel weg. Steht sie nicht dort, geben Sie den Zettel ab.',
    )
  })

  it('names its two ways out', () => {
    mountDialog()

    expect(textOf('.send-failed-twice-dialog .written-down')).toBe('Bestellung ist aufgeschrieben')
    expect(textOf('.send-failed-twice-dialog .try-again')).toBe('Noch einmal versuchen')
  })
})

describe('the dialog after a second failed send, in English', () => {
  it('names what happened in its heading', () => {
    mountDialog('en')

    expect(textOf('.send-failed-twice-dialog .title')).toBe('The order was not sent')
  })

  it('admits that nobody can tell whether the order arrived', () => {
    mountDialog('en')

    expect(textOf('.send-failed-twice-dialog .what-happened')).toBe(
      'The phone could not reach the laptop twice. We do not know whether the order arrived anyway.',
    )
  })

  it('asks for the order on a slip of paper', () => {
    mountDialog('en')

    expect(textOf('.send-failed-twice-dialog .write-it-down')).toBe(
      'Write the order down on a slip of paper.',
    )
  })

  it('says how the tablet at the station settles whether the slip is needed', () => {
    mountDialog('en')

    expect(textOf('.send-failed-twice-dialog .check-the-tablet')).toBe(
      'Look at the tablet at the station. If the order is already there, throw the slip away. If it is not there, hand the slip over.',
    )
  })

  it('names its two ways out', () => {
    mountDialog('en')

    expect(textOf('.send-failed-twice-dialog .written-down')).toBe('The order is written down')
    expect(textOf('.send-failed-twice-dialog .try-again')).toBe('Try again')
  })
})

describe('leaving the dialog', () => {
  it('reports the written slip only when the waiter says so', async () => {
    const dialog = mountDialog()

    ;(document.querySelector('.send-failed-twice-dialog .written-down') as HTMLElement).click()
    await dialog.vm.$nextTick()

    expect(dialog.emitted('writtenDown')).toHaveLength(1)
  })

  it('asks for another attempt when the waiter says so', async () => {
    const dialog = mountDialog()

    ;(document.querySelector('.send-failed-twice-dialog .try-again') as HTMLElement).click()
    await dialog.vm.$nextTick()

    expect(dialog.emitted('tryAgain')).toHaveLength(1)
  })

  it('cannot be left by tapping the screen beside it', async () => {
    const dialog = mountDialog()

    ;(document.querySelector('.v-overlay__scrim') as HTMLElement).click()
    await dialog.vm.$nextTick()

    expect(document.querySelector('.send-failed-twice-dialog .written-down')).not.toBeNull()
  })
})
