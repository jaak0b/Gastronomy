import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminStaffStore } from '../../src/stores/admin/staff'

const NEW_STAFF_MEMBER_ID = '77777777-7777-7777-7777-777777777777'

const ANNA = {
  staffMemberId: '33333333-3333-3333-3333-333333333333',
  name: 'Anna',
  isActive: true,
  hasDevice: true,
  lastSeenAtUtc: '2026-09-05T19:00:00Z',
  hasOutstandingInvitation: false,
}

const BERND = {
  staffMemberId: NEW_STAFF_MEMBER_ID,
  name: 'Bernd',
  isActive: true,
  hasDevice: false,
  lastSeenAtUtc: null,
  hasOutstandingInvitation: false,
}

interface RecordedCall {
  url: string
  method: string
  body: unknown
}

function stubLaptopThatAdds(): RecordedCall[] {
  const calls: RecordedCall[] = []
  let wasAdded = false
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calls.push({
        url,
        method: init?.method ?? 'GET',
        body: init?.body === undefined ? null : JSON.parse(String(init.body)),
      })
      if (init?.method === 'POST') {
        wasAdded = true
        return new Response(JSON.stringify({ id: NEW_STAFF_MEMBER_ID, name: 'Bernd' }), {
          status: 201,
        })
      }
      return new Response(JSON.stringify({ staffMembers: wasAdded ? [ANNA, BERND] : [ANNA] }), {
        status: 200,
      })
    }),
  )
  return calls
}

function stubLaptopThatRefuses(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
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
      return new Response(JSON.stringify({ staffMembers: [ANNA] }), { status: 200 })
    }),
  )
}

describe('adding a person to the waiter list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks the laptop to add the person under the name that was typed', async () => {
    const calls = stubLaptopThatAdds()
    const staff = useAdminStaffStore()

    await staff.create('Bernd')

    expect(calls).toContainEqual({
      url: '/api/admin/staff-members',
      method: 'POST',
      body: { name: 'Bernd' },
    })
  })

  it('hands back the id the laptop gave the person, so a QR code can name them', async () => {
    stubLaptopThatAdds()
    const staff = useAdminStaffStore()

    expect(await staff.create('Bernd')).toBe(NEW_STAFF_MEMBER_ID)
  })

  it('has the new person on the list straight away', async () => {
    stubLaptopThatAdds()
    const staff = useAdminStaffStore()
    await staff.load()

    await staff.create('Bernd')

    expect(staff.staffMembers.map((staffMember) => staffMember.name)).toEqual(['Anna', 'Bernd'])
  })

  it('holds the reason the laptop gave when it would not add the person', async () => {
    stubLaptopThatRefuses()
    const staff = useAdminStaffStore()

    await staff.create('')

    expect(staff.errorMessage).toEqual({
      key: 'admin.staff.nameMissing',
      parameters: {},
      count: null,
    })
  })

  it('hands back nothing when the person was not added, so no QR code is asked for', async () => {
    stubLaptopThatRefuses()
    const staff = useAdminStaffStore()

    expect(await staff.create('')).toBeNull()
  })
})
