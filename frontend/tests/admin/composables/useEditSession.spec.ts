import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { useEditSession, type EditSessionMode, type EditSessionOptions } from '../../../src/admin/composables/useEditSession'
import { adminFailed, adminOk, type AdminActionResult } from '../../../src/admin/core/adminActionResult'
import { adminErrorMessageForKey } from '../../../src/admin/core/adminErrorMessage'
import { testPlugins } from '../../support/plugins'

interface Station {
  stationId: string
  name: string
}

function mountTheSession(options: EditSessionOptions<Station, string, string>) {
  let session: ReturnType<typeof useEditSession<Station, string, string>> | null = null
  mount(
    defineComponent({
      setup() {
        session = useEditSession(options)
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins('de') } },
  )
  if (session === null) {
    throw new Error('The edit session did not set up.')
  }
  return session as ReturnType<typeof useEditSession<Station, string, string>>
}

const KITCHEN: Station = { stationId: 'station-kueche', name: 'Küche' }

function savedAs(value: string): () => Promise<AdminActionResult<string>> {
  return async () => adminOk(value)
}

function refusedWith(messageKey: string): () => Promise<AdminActionResult<string>> {
  return async () => adminFailed(adminErrorMessageForKey(messageKey, {}))
}

describe('an edit session of the admin screens', () => {
  it('is closed until the admin opens it', () => {
    const session = mountTheSession({ saveThrough: savedAs('saved') })

    expect(session.isOpen.value).toBe(false)
  })

  it('opens empty for something new', () => {
    const session = mountTheSession({ saveThrough: savedAs('saved') })

    session.openForCreate()

    expect({ isOpen: session.isOpen.value, edited: session.edited.value }).toEqual({ isOpen: true, edited: null })
  })

  it('opens holding what the admin chose to edit', () => {
    const session = mountTheSession({ saveThrough: savedAs('saved') })

    session.openForEdit(KITCHEN)

    expect(session.edited.value).toEqual(KITCHEN)
  })

  it('hands the draft and the edited entry to the action that saves', async () => {
    const received: [string, Station | null][] = []
    const session = mountTheSession({
      saveThrough: async (draft, edited) => {
        received.push([draft, edited])
        return adminOk('saved')
      },
    })
    session.openForEdit(KITCHEN)

    await session.save('Küche innen')

    expect(received).toEqual([['Küche innen', KITCHEN]])
  })

  it('closes once the laptop accepted and passes on what it answered for something new', async () => {
    const answered: [string, EditSessionMode][] = []
    const session = mountTheSession({
      saveThrough: savedAs('station-neu'),
      afterSaving: (saved, mode) => {
        answered.push([saved, mode])
      },
    })
    session.openForCreate()

    await session.save('Bar')

    expect({ isOpen: session.isOpen.value, answered }).toEqual({ isOpen: false, answered: [['station-neu', 'create']] })
  })

  it('says the saved entry was an edit when the admin opened an existing one', async () => {
    const answered: [string, EditSessionMode][] = []
    const session = mountTheSession({
      saveThrough: savedAs('station-kueche'),
      afterSaving: (saved, mode) => {
        answered.push([saved, mode])
      },
    })
    session.openForEdit(KITCHEN)

    await session.save('Küche innen')

    expect(answered).toEqual([['station-kueche', 'edit']])
  })

  it('stays open and names the refusal when the laptop refused', async () => {
    const session = mountTheSession({ saveThrough: refusedWith('errors.admin.actionFailed') })
    session.openForCreate()

    await session.save('Bar')

    expect({ isOpen: session.isOpen.value, refusal: session.refusalText.value }).toEqual({
      isOpen: true,
      refusal: 'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
    })
  })

  it('forgets the refusal when the admin closes it', async () => {
    const session = mountTheSession({ saveThrough: refusedWith('errors.admin.actionFailed') })
    session.openForCreate()
    await session.save('Bar')

    session.close()

    expect(session.refusalText.value).toBeNull()
  })

  it('saves nothing while it is closed', async () => {
    const received: string[] = []
    const session = mountTheSession({
      saveThrough: async (draft) => {
        received.push(draft)
        return adminOk('saved')
      },
    })

    await session.save('Bar')

    expect(received).toEqual([])
  })
})
