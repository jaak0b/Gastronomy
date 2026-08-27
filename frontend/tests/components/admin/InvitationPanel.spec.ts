import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import InvitationPanel from '../../../src/components/admin/staff/InvitationPanel.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const INVITATION = {
  invitationId: 'invitation-1',
  qrUrl: 'http://192.168.1.20:5000/j/abc123',
  sixDigitCode: '482913',
  expiresAtUtc: '2026-08-27T18:05:00Z',
  staffMember: null,
}

function mountPanel() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(InvitationPanel, {
    props: { invitation: INVITATION },
    global: { plugins: [i18n] },
  })
}

describe('the invitation panel', () => {
  it('shows a QR code image, because the phone scans it with its camera', () => {
    const panel = mountPanel()

    expect(panel.get('img.qr-image').attributes('src')).toBe(
      '/api/admin/enrolment/invitations/current/qr.svg',
    )
  })

  it('shows the six digit code for a camera that does not work', () => {
    const panel = mountPanel()

    expect(panel.get('.six-digit-code').text()).toBe('482913')
  })

  it('keeps the address as secondary text rather than as the main instruction', () => {
    const panel = mountPanel()

    expect(panel.get('.qr-url').text()).toBe('http://192.168.1.20:5000/j/abc123')
  })

  it('still walks the admin through the three steps', () => {
    const panel = mountPanel()

    expect(panel.get('ol').text()).toContain(
      'Der Kellner scannt diesen QR-Code mit der Kamera seines Telefons.',
    )
  })
})
