import { assertNever } from './assertNever'
import type { AdminErrorMessage } from './adminErrorMessage'

export type InvitationQr =
  | { kind: 'loading' }
  | { kind: 'ready'; imageUrl: string }
  | { kind: 'gone'; message: AdminErrorMessage }
  | { kind: 'unreachable' }

export interface InvitationQrView {
  imageUrl: string | null
  messageKey: string | null
}

export const QR_UNREACHABLE_KEY = 'admin.enrol.qrUnreachable'

export function invitationQrView(qr: InvitationQr): InvitationQrView {
  switch (qr.kind) {
    case 'loading':
      return { imageUrl: null, messageKey: null }
    case 'ready':
      return { imageUrl: qr.imageUrl, messageKey: null }
    case 'gone':
      return { imageUrl: null, messageKey: qr.message.key }
    case 'unreachable':
      return { imageUrl: null, messageKey: QR_UNREACHABLE_KEY }
    default:
      return assertNever(qr)
  }
}
