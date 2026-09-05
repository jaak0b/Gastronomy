import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fetchInvitationQr, invitationQrPath } from '../../src/api/invitationQr'
import { useAdminStaffStore } from '../../src/stores/admin/staff'

const INVITATION_ID = '44444444-4444-4444-4444-444444444444'
const STAFF_MEMBER_ID = '33333333-3333-3333-3333-333333333333'
const SVG = '<svg width="176"></svg>'

function stubLaptop(qrResponse: () => Response) {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      if (url.endsWith('/qr.svg')) {
        return qrResponse()
      }
      if (url.endsWith('/invitations')) {
        return new Response(
          JSON.stringify({
            invitationId: INVITATION_ID,
            qrUrl: 'http://192.168.1.20:5000/j/CODE',
            expiresAtUtc: '2026-08-27T20:00:00Z',
            staffMember: null,
          }),
          { status: 201 },
        )
      }
      return new Response(JSON.stringify({ staffMembers: [] }), { status: 200 })
    }),
  )
  return urls
}

function renderedQr(): Response {
  return new Response(SVG, { status: 200, headers: { 'Content-Type': 'image/svg+xml' } })
}

describe('the address of an invitation QR code', () => {
  it('names the one invitation it belongs to, so a newer code cannot be shown in its place', () => {
    expect(invitationQrPath(INVITATION_ID)).toBe(
      `/api/admin/enrolment/invitations/${INVITATION_ID}/qr.svg`,
    )
  })
})

describe('fetching an invitation QR code', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('turns the rendered drawing into something an image tag can show', async () => {
    stubLaptop(renderedQr)

    const qr = await fetchInvitationQr(INVITATION_ID)

    expect(qr).toEqual({
      kind: 'ready',
      imageUrl: `data:image/svg+xml;charset=utf-8,${encodeURIComponent(SVG)}`,
    })
  })

  it('carries the laptop wording through when the code is no longer usable', async () => {
    stubLaptop(
      () =>
        new Response(
          JSON.stringify({ code: 'EnrolmentCodeAlreadyUsed', messageKey: 'admin.enrol.qrAlreadyUsed' }),
          { status: 410 },
        ),
    )

    const qr = await fetchInvitationQr(INVITATION_ID)

    expect(qr).toEqual({
      kind: 'gone',
      message: { key: 'admin.enrol.qrAlreadyUsed', parameters: {}, count: null },
    })
  })

  it('says the laptop could not be reached rather than showing a broken picture', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )

    expect(await fetchInvitationQr(INVITATION_ID)).toEqual({ kind: 'unreachable' })
  })
})

describe('creating an invitation from the admin screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetches the QR code of the invitation it just created', async () => {
    const urls = stubLaptop(renderedQr)
    const staff = useAdminStaffStore()

    await staff.createInvitation(STAFF_MEMBER_ID)

    expect(urls).toContain(`/api/admin/enrolment/invitations/${INVITATION_ID}/qr.svg`)
    expect(staff.invitationQr).toEqual({
      kind: 'ready',
      imageUrl: `data:image/svg+xml;charset=utf-8,${encodeURIComponent(SVG)}`,
    })
  })

  it('holds the refusal when the laptop will not render that code', async () => {
    stubLaptop(
      () =>
        new Response(
          JSON.stringify({ code: 'EnrolmentCodeExpired', messageKey: 'admin.enrol.expired' }),
          { status: 410 },
        ),
    )
    const staff = useAdminStaffStore()

    await staff.createInvitation()

    expect(staff.invitationQr).toEqual({
      kind: 'gone',
      message: { key: 'admin.enrol.expired', parameters: {}, count: null },
    })
  })

  it('drops the drawing again when the panel is closed', async () => {
    stubLaptop(renderedQr)
    const staff = useAdminStaffStore()
    await staff.createInvitation()

    staff.closeInvitation()

    expect(staff.invitation).toBeNull()
    expect(staff.invitationQr).toEqual({ kind: 'loading' })
  })
})
