import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fetchInvitationQr, invitationQrPath } from '../../../src/admin/api/invitationQrRequests'
import { useAdminEnrolmentStore } from '../../../src/admin/stores/enrolment'
import {
  answer,
  inTurn,
  noConnection,
  refusal,
  stubLaptop,
  type LaptopReply,
  type StubbedLaptop,
} from '../../support/laptop'

const INVITATION_ID = '44444444-4444-4444-4444-444444444444'
const STAFF_MEMBER_ID = '33333333-3333-3333-3333-333333333333'
const STATION_ID = '55555555-5555-5555-5555-555555555555'
const SVG = '<svg width="176"></svg>'

function invitationLaptop(qrReply: LaptopReply): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({ staffMembers: [] }))
    .answers(
      'ANY',
      /\/invitations$/,
      answer(
        {
          invitationId: INVITATION_ID,
          qrUrl: 'http://192.168.1.20:5000/j/CODE',
          expiresAtUtc: '2026-08-27T20:00:00Z',
          staffMember: null,
          station: null,
        },
        201,
      ),
    )
    .answers('GET', /\/qr\.svg$/, qrReply)
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
    invitationLaptop(renderedQr)

    const qr = await fetchInvitationQr(INVITATION_ID)

    expect(qr).toEqual({
      kind: 'ready',
      imageUrl: `data:image/svg+xml;charset=utf-8,${encodeURIComponent(SVG)}`,
    })
  })

  it('carries the laptop wording through when the code is no longer usable', async () => {
    invitationLaptop(
      () =>
        new Response(
          JSON.stringify({
            code: 'EnrolmentCodeAlreadyUsed',
            messageKey: 'errors.enrolment.qrAlreadyUsed',
            parameters: {},
            details: null,
          }),
          { status: 410 },
        ),
    )

    const qr = await fetchInvitationQr(INVITATION_ID)

    expect(qr).toEqual({
      kind: 'gone',
      message: { key: 'errors.enrolment.qrAlreadyUsed', parameters: {}, count: null },
    })
  })

  it('says the laptop could not be reached rather than showing a broken picture', async () => {
    stubLaptop().answersEverythingElse(noConnection())

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
    const laptop = invitationLaptop(renderedQr)
    const enrolment = useAdminEnrolmentStore()

    await enrolment.createInvitation({ kind: 'staffMember', staffMemberId: STAFF_MEMBER_ID })

    expect(laptop.urls()).toContain(`/api/admin/enrolment/invitations/${INVITATION_ID}/qr.svg`)
    expect(enrolment.invitationQr).toEqual({
      kind: 'ready',
      imageUrl: `data:image/svg+xml;charset=utf-8,${encodeURIComponent(SVG)}`,
    })
  })

  it('names the waiter the code belongs to and nothing else', async () => {
    const laptop = invitationLaptop(renderedQr)
    const enrolment = useAdminEnrolmentStore()

    await enrolment.createInvitation({ kind: 'staffMember', staffMemberId: STAFF_MEMBER_ID })

    expect(laptop.writtenBodies()).toEqual([{ staffMemberId: STAFF_MEMBER_ID }])
  })

  it('names nobody when the waiter is not on the list yet', async () => {
    const laptop = invitationLaptop(renderedQr)
    const enrolment = useAdminEnrolmentStore()

    await enrolment.createInvitation({ kind: 'somebodyNew' })

    expect(laptop.writtenBodies()).toEqual([{}])
  })

  it('names the station the code belongs to and nothing else', async () => {
    const laptop = invitationLaptop(renderedQr)
    const enrolment = useAdminEnrolmentStore()

    await enrolment.createInvitation({ kind: 'station', stationId: STATION_ID })

    expect(laptop.writtenBodies()).toEqual([{ stationId: STATION_ID }])
  })

  it('holds the refusal when the laptop will not render that code', async () => {
    invitationLaptop(
      () =>
        new Response(
          JSON.stringify({
            code: 'EnrolmentCodeExpired',
            messageKey: 'errors.enrolment.qrExpired',
            parameters: {},
            details: null,
          }),
          { status: 410 },
        ),
    )
    const enrolment = useAdminEnrolmentStore()

    await enrolment.createInvitation({ kind: 'staffMember', staffMemberId: STAFF_MEMBER_ID })

    expect(enrolment.invitationQr).toEqual({
      kind: 'gone',
      message: { key: 'errors.enrolment.qrExpired', parameters: {}, count: null },
    })
  })

  it('takes the previous code off the screen when the laptop would not create a new one', async () => {
    stubLaptop()
      .answers(
        'ANY',
        /\/invitations$/,
        inTurn(
          answer(
            {
              invitationId: INVITATION_ID,
              qrUrl: 'http://192.168.1.20:5000/j/CODE',
              expiresAtUtc: '2026-08-27T20:00:00Z',
              staffMember: null,
              station: null,
            },
            201,
          ),
          refusal('errors.admin.actionFailed'),
        ),
      )
      .answers('ANY', /\/qr\.svg$/, () => renderedQr())
    const enrolment = useAdminEnrolmentStore()
    await enrolment.createInvitation({ kind: 'staffMember', staffMemberId: STAFF_MEMBER_ID })

    await enrolment.createInvitation({ kind: 'staffMember', staffMemberId: STAFF_MEMBER_ID })

    expect(enrolment.invitation).toBeNull()
  })

  it('says why the laptop would not create the code', async () => {
    stubLaptop().answersEverythingElse(
      refusal('errors.enrolment.atMostOneOwner', {
        status: 400,
        code: 'ValidationFailed',
      }),
    )
    const enrolment = useAdminEnrolmentStore()

    const result = await enrolment.createInvitation({
      kind: 'staffMember',
      staffMemberId: STAFF_MEMBER_ID,
    })

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.enrolment.atMostOneOwner', parameters: {}, count: null },
    })
  })

  it('drops the drawing again when the panel is closed', async () => {
    invitationLaptop(renderedQr)
    const enrolment = useAdminEnrolmentStore()
    await enrolment.createInvitation({ kind: 'staffMember', staffMemberId: STAFF_MEMBER_ID })

    enrolment.closeInvitation()

    expect(enrolment.invitation).toBeNull()
    expect(enrolment.invitationQr).toEqual({ kind: 'loading' })
  })
})
