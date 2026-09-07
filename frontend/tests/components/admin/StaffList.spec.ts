import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import StaffList from '../../../src/components/admin/staff/StaffList.vue'
import { useConnectionStore } from '../../../src/stores/connection'
import { pressInDialog, testPlugins, waitForDialog } from '../../support/plugins'

const STAFF_MEMBER_ID = '33333333-3333-3333-3333-333333333333'

const ONE_STAFF_MEMBER = {
  staffMembers: [
    {
      staffMemberId: STAFF_MEMBER_ID,
      name: 'Anna',
      isActive: true,
      hasDevice: true,
      lastSeenAtUtc: '2026-08-27T19:00:00Z',
      hasOutstandingInvitation: false,
    },
  ],
}

function stubFetch(revokeStatus = 200) {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      urls.push(url)
      if (url.endsWith('/revoke-device')) {
        return new Response(
          JSON.stringify({ code: 'NotFound', messageKey: 'admin.personHasNoPhone' }),
          { status: revokeStatus },
        )
      }
      return new Response(JSON.stringify(ONE_STAFF_MEMBER), { status: 200 })
    }),
  )
  return urls
}

function mountList() {
  return mount(StaffList, { global: { plugins: testPlugins() }, attachTo: document.body })
}

async function firstStaffMember(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('.staff-row').exists()).toBe(true))
}

const OFF_THE_LIST = {
  staffMembers: [{ ...ONE_STAFF_MEMBER.staffMembers[0], isActive: false }],
}

function stubFetchWith(staffMembers: unknown) {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      return new Response(JSON.stringify(staffMembers), { status: 200 })
    }),
  )
  return urls
}

describe('taking a staff member off the list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks before it happens', async () => {
    const urls = stubFetchWith(ONE_STAFF_MEMBER)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(urls.some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('says that the orders already placed are kept', async () => {
    stubFetchWith(ONE_STAFF_MEMBER)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(document.querySelector('.confirm-body')!.textContent).toContain('bleiben gespeichert')
  })

  it('takes them off the list once the question is answered with yes', async () => {
    const urls = stubFetchWith(ONE_STAFF_MEMBER)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/staff-members/${STAFF_MEMBER_ID}/deactivate`),
    )
  })
})

describe('a staff member taken off the list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.show-deactivated').exists()).toBe(true))

    expect(list.find('.staff-row').exists()).toBe(false)
  })

  it('says so on their row once it is shown', async () => {
    stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await firstStaffMember(list)

    expect(list.get('.deactivated').text()).toBe('Deaktiviert')
  })

  it('puts them back on the list without asking a question first', async () => {
    const urls = stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await firstStaffMember(list)
    await list.get('.reactivate').trigger('click')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/staff-members/${STAFF_MEMBER_ID}/activate`),
    )
  })
})

describe('the QR code for setting up a phone', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function stubInvitationFor(staffMember: unknown) {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const payload = url.includes('/invitations')
          ? {
              invitationId: 'invitation-1',
              qrUrl: 'http://192.168.0.22:5000/j/CODE',
              sixDigitCode: '158026',
              expiresAtUtc: '2026-08-27T20:00:00Z',
              staffMember,
              station: null,
            }
          : ONE_STAFF_MEMBER
        return new Response(JSON.stringify(payload), { status: 200 })
      }),
    )
  }

  it('sits inside the row of the staff member it belongs to', async () => {
    stubInvitationFor({ id: STAFF_MEMBER_ID, name: 'Anna' })

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.new-code').trigger('click')

    await vi.waitFor(() =>
      expect(list.get('.staff-row').find('.invitation-panel').exists()).toBe(true),
    )
  })

  it('sits on its own when it belongs to nobody yet', async () => {
    stubInvitationFor(null)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.new-staff-member').trigger('click')

    await vi.waitFor(() => expect(list.find('.invitation-panel').exists()).toBe(true))
    expect(list.get('.staff-row').find('.invitation-panel').exists()).toBe(false)
  })
})

describe('adding somebody new to the waiter list', () => {
  interface RecordedCall {
    url: string
    method: string
    body: unknown
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks the laptop for a QR code that names nobody, because the waiter types their own name', async () => {
    const calls: RecordedCall[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        calls.push({
          url,
          method: init?.method ?? 'GET',
          body: init?.body === undefined ? null : JSON.parse(String(init.body)),
        })
        if (url.includes('/qr.svg')) {
          return new Response('<svg></svg>', {
            status: 200,
            headers: { 'Content-Type': 'image/svg+xml' },
          })
        }
        if (url.endsWith('/invitations')) {
          return new Response(
            JSON.stringify({
              invitationId: 'invitation-1',
              qrUrl: 'http://192.168.0.22:5000/j/CODE',
              expiresAtUtc: '2026-08-27T20:00:00Z',
              staffMember: null,
              station: null,
            }),
            { status: 201 },
          )
        }
        return new Response(JSON.stringify(ONE_STAFF_MEMBER), { status: 200 })
      }),
    )

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.new-staff-member').trigger('click')

    await vi.waitFor(() =>
      expect(calls).toContainEqual({
        url: '/api/admin/enrolment/invitations',
        method: 'POST',
        body: {},
      }),
    )
  })
})

describe('two refusals one after the other', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the newest one, not the one the admin has already read', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (url.endsWith('/invitations')) {
          return new Response(
            JSON.stringify({
              code: 'ValidationFailed',
              messageKey: 'enrolment.atMostOneOwner',
              parameters: {},
              details: null,
            }),
            { status: 400 },
          )
        }
        if (init?.method === 'PUT') {
          return new Response(
            JSON.stringify({
              code: 'ValidationFailed',
              messageKey: 'admin.staff.nameMissing',
              parameters: {},
              details: null,
            }),
            { status: 400 },
          )
        }
        return new Response(JSON.stringify(ONE_STAFF_MEMBER), { status: 200 })
      }),
    )

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.new-code').trigger('click')
    await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))
    await list.get('.rename').trigger('click')
    await list.get('.rename-field input').setValue('Anna B')
    await list.get('.save-name').trigger('click')

    await vi.waitFor(() =>
      expect(list.get('.refusal').text()).toBe(
        'Geben Sie dem Kellner einen Namen, bevor Sie speichern.',
      ),
    )
  })
})

describe('the waiter screen the admin has left', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('no longer reloads the list when the laptop reports a change', async () => {
    const urls = stubFetchWith(ONE_STAFF_MEMBER)
    const list = mountList()
    await firstStaffMember(list)

    list.unmount()
    urls.length = 0
    await useConnectionStore().refetchAll()

    expect(urls).toEqual([])
  })
})

describe('a staff member in the admin list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is shown as having a phone when the laptop says a device is enrolled', async () => {
    stubFetch()

    const list = mountList()
    await firstStaffMember(list)

    expect(list.find('.no-phone').exists()).toBe(false)
  })

  it('is deactivated at their own address', async () => {
    const urls = stubFetch()

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/staff-members/${STAFF_MEMBER_ID}/deactivate`),
    )
  })

})

describe('a staff member who already has a phone', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('keeps the name and the buttons on one line', async () => {
    stubFetch()

    const list = mountList()
    await firstStaffMember(list)

    const row = list.get('.staff-row-line')

    expect(row.find('.name').exists()).toBe(true)
    expect(row.find('.new-code').exists()).toBe(true)
    expect(row.find('.rename').exists()).toBe(true)
    expect(row.find('.deactivate').exists()).toBe(true)
  })
})

describe('the length of a waiter name the admin types', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stops where the laptop stops storing it', async () => {
    stubFetchWith(ONE_STAFF_MEMBER)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('.rename').trigger('click')

    expect(list.get('.rename-field input').attributes('maxlength')).toBe('40')
  })
})
