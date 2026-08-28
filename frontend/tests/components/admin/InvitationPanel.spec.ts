import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import InvitationPanel from '../../../src/components/admin/staff/InvitationPanel.vue'
import { testPlugins } from '../../support/plugins'

const INVITATION = {
  invitationId: 'invitation-1',
  qrUrl: 'http://192.168.1.20:5000/j/abc123',
  expiresAtUtc: '2026-08-27T18:05:00Z',
  staffMember: null,
}

function mountPanel() {
  return mount(InvitationPanel, {
    props: { invitation: INVITATION },
    global: { plugins: testPlugins() },
  })
}

describe('the invitation panel', () => {
  it('shows a QR code image, because the phone scans it with its camera', () => {
    const panel = mountPanel()

    expect(panel.get('img.qr-image').attributes('src')).toBe(
      '/api/admin/enrolment/invitations/current/qr.svg',
    )
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
      'Scannen Sie diesen QR-Code mit der Kamera des Telefons. Geben Sie danach am Telefon den Namen ein.',
    )
  })

  it('drops the name step when the code belongs to somebody already', () => {
    const panel = mount(InvitationPanel, {
      props: {
        invitation: { ...INVITATION, staffMember: { id: 'staff-1', name: 'Anna' } },
      },
      global: { plugins: testPlugins() },
    })

    expect(panel.get('.instruction').text()).toBe(
      'Scannen Sie diesen QR-Code mit der Kamera des Telefons.',
    )
  })
})
