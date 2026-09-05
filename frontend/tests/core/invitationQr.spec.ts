import { describe, expect, it } from 'vitest'
import de from '../../src/locales/de.json'
import en from '../../src/locales/en.json'
import { invitationQrView, QR_UNREACHABLE_KEY } from '../../src/core/invitationQr'

describe('what the admin panel shows for an invitation QR code', () => {
  it('shows the picture once the laptop has rendered it', () => {
    const view = invitationQrView({ kind: 'ready', imageUrl: 'data:image/svg+xml,%3Csvg%3E' })

    expect(view).toEqual({ imageUrl: 'data:image/svg+xml,%3Csvg%3E', messageKey: null })
  })

  it('shows neither a picture nor a message while it is still being fetched', () => {
    expect(invitationQrView({ kind: 'loading' })).toEqual({ imageUrl: null, messageKey: null })
  })

  it('shows the wording the laptop sent instead of a picture when the code is no longer usable', () => {
    const view = invitationQrView({
      kind: 'gone',
      message: { key: 'admin.enrol.qrAlreadyUsed', parameters: {}, count: null },
    })

    expect(view).toEqual({ imageUrl: null, messageKey: 'admin.enrol.qrAlreadyUsed' })
  })

  it('says the laptop did not answer when the request never arrived', () => {
    expect(invitationQrView({ kind: 'unreachable' })).toEqual({
      imageUrl: null,
      messageKey: QR_UNREACHABLE_KEY,
    })
  })
})

describe('every reason the panel can name', () => {
  const KEYS = [
    'qrAlreadyUsed',
    'qrReplaced',
    'qrUnavailable',
    'qrUnreachable',
    'expired',
    'newQrCode',
  ] as const

  it('is worded in German', () => {
    const missing = KEYS.filter((key) => (de.admin.enrol as Record<string, string>)[key] === undefined)

    expect(missing).toEqual([])
  })

  it('is worded in English', () => {
    const missing = KEYS.filter((key) => (en.admin.enrol as Record<string, string>)[key] === undefined)

    expect(missing).toEqual([])
  })
})
