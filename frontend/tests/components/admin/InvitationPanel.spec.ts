import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import InvitationPanel from '../../../src/components/admin/enrolment/InvitationPanel.vue'
import type { InvitationQr } from '../../../src/core/invitationQr'
import type { Invitation } from '../../../src/core/apiTypes'
import { testPlugins } from '../../support/plugins'

const INVITATION: Invitation = {
  invitationId: 'invitation-1',
  qrUrl: 'http://192.168.1.20:5000/j/abc123',
  expiresAtUtc: '2026-08-27T18:05:00Z',
  staffMember: { id: 'staff-1', name: 'Anna' },
  station: null,
}

const QR_IMAGE_URL = 'data:image/svg+xml;charset=utf-8,%3Csvg%3E'
const READY: InvitationQr = { kind: 'ready', imageUrl: QR_IMAGE_URL }

function mountPanel(qr: InvitationQr = READY, invitation: Invitation = INVITATION) {
  return mount(InvitationPanel, {
    props: { invitation, qr },
    global: { plugins: testPlugins() },
  })
}

describe('the invitation panel', () => {
  it('shows the QR code the laptop rendered for this one invitation', () => {
    const panel = mountPanel()

    expect(panel.get('img.qr-image').attributes('src')).toBe(QR_IMAGE_URL)
  })

  it('shows the address exactly once, as a code block', () => {
    const panel = mountPanel()

    expect(panel.findAll('.qr-url')).toHaveLength(1)
    expect(panel.get('code.qr-url').text()).toBe('http://192.168.1.20:5000/j/abc123')
  })

  it('offers a copy button beside the address, because nobody types that by hand', () => {
    const panel = mountPanel()

    expect(panel.find('.copy-url').exists()).toBe(true)
  })

  it('tells the admin what happens, before showing the code', () => {
    const panel = mountPanel()

    expect(panel.get('.instruction').text()).toBe(
      'Scannen Sie diesen QR-Code mit der Kamera des Telefons.',
    )
  })
})

describe('the copy button beside the address', () => {
  afterEach(() => {
    Reflect.deleteProperty(navigator, 'clipboard')
  })

  function offerAClipboard(written: string[]): void {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: async (text: string) => {
          written.push(text)
        },
      },
    })
  }

  it('hands the address to the clipboard when the browser offers one', async () => {
    const written: string[] = []
    offerAClipboard(written)
    const panel = mountPanel()

    await panel.get('.copy-url').trigger('click')

    expect(written).toEqual(['http://192.168.1.20:5000/j/abc123'])
  })

  it('says nothing while nobody has pressed it', () => {
    const panel = mountPanel()

    expect(panel.find('.copy-unavailable').exists()).toBe(false)
  })

  it('says to type the address instead when the browser offers no clipboard', async () => {
    const panel = mountPanel()

    await panel.get('.copy-url').trigger('click')

    expect(panel.get('.copy-unavailable').text()).toBe(
      'Tippen Sie die Adresse oben von Hand ab. Das Kopieren funktioniert auf dieser Seite nicht.',
    )
  })

  it('says the same in English', async () => {
    const panel = mount(InvitationPanel, {
      props: { invitation: INVITATION, qr: READY },
      global: { plugins: testPlugins('en') },
    })

    await panel.get('.copy-url').trigger('click')

    expect(panel.get('.copy-unavailable').text()).toBe(
      'Type the address above by hand. Copying does not work on this page.',
    )
  })
})

describe('the same panel used for the tablet of a station', () => {
  const STATION_INVITATION: Invitation = {
    ...INVITATION,
    staffMember: null,
    station: { id: 'station-kueche', name: 'Küche' },
  }

  it('names the station whose tablet is to scan the code', () => {
    const panel = mountPanel(READY, STATION_INVITATION)

    expect(panel.get('.instruction').text()).toBe(
      'Scannen Sie diesen QR-Code mit der Kamera des Tablets an der Ausgabestelle Küche.',
    )
  })

  it('says it is setting up a tablet rather than a phone', () => {
    const panel = mountPanel(READY, STATION_INVITATION)

    expect(panel.get('.v-card-title').text()).toBe('Tablet einrichten')
  })

  it('shows the same QR code and the same address as for a phone', () => {
    const panel = mountPanel(READY, STATION_INVITATION)

    expect(panel.get('img.qr-image').attributes('src')).toBe(QR_IMAGE_URL)
    expect(panel.get('code.qr-url').text()).toBe('http://192.168.1.20:5000/j/abc123')
  })

  it('says it is setting up a phone when no station is named', () => {
    const panel = mountPanel()

    expect(panel.get('.v-card-title').text()).toBe('Telefon einrichten')
  })
})

describe('an invitation the laptop will not render a QR code for', () => {
  it('says in plain German that a device has already used it', () => {
    const panel = mountPanel({
      kind: 'gone',
      message: { key: 'admin.enrol.qrAlreadyUsed', parameters: {}, count: null },
    })

    expect(panel.get('.qr-gone').text()).toBe(
      'Erstellen Sie einen neuen QR-Code. Dieser wurde schon von einem Gerät benutzt.',
    )
  })

  it('says in plain English that a device has already used it', () => {
    const panel = mount(InvitationPanel, {
      props: {
        invitation: INVITATION,
        qr: {
          kind: 'gone',
          message: { key: 'admin.enrol.qrAlreadyUsed', parameters: {}, count: null },
        },
      },
      global: { plugins: testPlugins('en') },
    })

    expect(panel.get('.qr-gone').text()).toBe(
      'Create a new QR code. This one has already been used by a device.',
    )
  })

  it('says that it ran out of time when that is the reason', () => {
    const panel = mountPanel({
      kind: 'gone',
      message: { key: 'admin.enrol.expired', parameters: {}, count: null },
    })

    expect(panel.get('.qr-gone').text()).toBe(
      'Erstellen Sie einen neuen QR-Code. Dieser wurde fünf Minuten lang nicht gescannt.',
    )
  })

  it('says so rather than showing a picture or an address nobody can use', () => {
    const panel = mountPanel({
      kind: 'gone',
      message: { key: 'admin.enrol.qrUnavailable', parameters: {}, count: null },
    })

    expect(panel.find('img.qr-image').exists()).toBe(false)
    expect(panel.find('.qr-url').exists()).toBe(false)
  })

  it('offers the button that creates a new one', () => {
    const panel = mountPanel({
      kind: 'gone',
      message: { key: 'admin.enrol.qrUnavailable', parameters: {}, count: null },
    })

    expect(panel.get('.renew-code').text()).toBe('Neuen QR-Code erstellen')
  })

  it('asks for a new one when that button is pressed', async () => {
    const panel = mountPanel({
      kind: 'gone',
      message: { key: 'admin.enrol.qrUnavailable', parameters: {}, count: null },
    })

    await panel.get('.renew-code').trigger('click')

    expect(panel.emitted('renew')).toHaveLength(1)
  })

  it('says the laptop did not answer when the request never arrived', () => {
    const panel = mountPanel({ kind: 'unreachable' })

    expect(panel.get('.qr-gone').text()).toBe(
      'Laden Sie die Seite neu. Der Laptop hat den QR-Code nicht geliefert.',
    )
  })
})

describe('an invitation whose QR code is still on its way', () => {
  it('shows no picture yet and no message either', () => {
    const panel = mountPanel({ kind: 'loading' })

    expect(panel.find('img.qr-image').exists()).toBe(false)
    expect(panel.find('.qr-gone').exists()).toBe(false)
  })
})
