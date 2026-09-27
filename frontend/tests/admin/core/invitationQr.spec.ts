import { describe, expect, it } from 'vitest'
import de from '../../../src/shared/i18n/de.json'
import en from '../../../src/shared/i18n/en.json'
import { invitationQrView, QR_UNREACHABLE_KEY } from '../../../src/admin/core/invitationQr'

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
      message: { key: 'errors.enrolment.qrAlreadyUsed', parameters: {}, count: null },
    })

    expect(view).toEqual({ imageUrl: null, messageKey: 'errors.enrolment.qrAlreadyUsed' })
  })

  it('says the laptop did not answer when the request never arrived', () => {
    expect(invitationQrView({ kind: 'unreachable' })).toEqual({
      imageUrl: null,
      messageKey: QR_UNREACHABLE_KEY,
    })
  })
})

interface LocaleTree {
  [key: string]: string | LocaleTree
}

function wordingAt(tree: LocaleTree, path: string): string | undefined {
  let node: string | LocaleTree | undefined = tree
  for (const part of path.split('.')) {
    node = typeof node === 'object' ? node[part] : undefined
  }
  return typeof node === 'string' ? node : undefined
}

describe('every reason the panel can name', () => {
  const KEYS = [
    'errors.enrolment.qrAlreadyUsed',
    'errors.enrolment.qrReplaced',
    'errors.enrolment.qrUnavailable',
    QR_UNREACHABLE_KEY,
    'errors.enrolment.qrExpired',
    'admin.enrolment.actions.newQrCode',
  ]

  it('is worded in German', () => {
    const missing = KEYS.filter((key) => wordingAt(de, key) === undefined)

    expect(missing).toEqual([])
  })

  it('is worded in English', () => {
    const missing = KEYS.filter((key) => wordingAt(en, key) === undefined)

    expect(missing).toEqual([])
  })
})
