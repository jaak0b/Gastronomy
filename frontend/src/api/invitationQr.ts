import { adminErrorMessage } from '../core/adminErrorMessage'
import { isApiErrorBody } from '../core/apiError'
import type { InvitationQr } from '../core/invitationQr'

export function invitationQrPath(invitationId: string): string {
  return `/api/admin/enrolment/invitations/${invitationId}/qr.svg`
}

function imageUrlFor(svg: string): string {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`
}

function refusalFrom(payload: string): InvitationQr {
  let parsed: unknown = null
  try {
    parsed = JSON.parse(payload)
  } catch {
    parsed = null
  }
  return { kind: 'gone', message: adminErrorMessage(isApiErrorBody(parsed) ? parsed : null) }
}

export async function fetchInvitationQr(invitationId: string): Promise<InvitationQr> {
  let response: Response
  try {
    response = await fetch(invitationQrPath(invitationId))
  } catch {
    return { kind: 'unreachable' }
  }
  const payload = await response.text()
  return response.ok ? { kind: 'ready', imageUrl: imageUrlFor(payload) } : refusalFrom(payload)
}
