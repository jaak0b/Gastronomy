import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { answer, inTurn, refusal, stubLaptop, type StubbedLaptop } from '../../support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../src/shared/stores/connection')
const { useAdminStaffStore } = await import('../../../src/admin/stores/staff')

const ANNA = { staffMemberId: 'staff-anna', name: 'Anna', isActive: true, hasDevice: true }
const BEN = { staffMemberId: 'staff-ben', name: 'Ben', isActive: true, hasDevice: false }

function staffLaptop(...lists: unknown[][]): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', /./, inTurn(...lists.map((staffMembers) => answer({ staffMembers }))))
}

beforeEach(() => {
  setActivePinia(createPinia())
  forgetHubEvents()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the waiter list of the admin', () => {
  it('holds the waiters the laptop listed', async () => {
    staffLaptop([ANNA, BEN])
    const staff = useAdminStaffStore()

    await staff.load()

    expect(staff.staffMembers).toEqual([ANNA, BEN])
    expect(staff.loadFailed).toBe(false)
  })

  it('says the load failed when the laptop refused it', async () => {
    stubLaptop().answers('GET', /./, answer({}, 500))
    const staff = useAdminStaffStore()

    await staff.load()

    expect(staff.loadFailed).toBe(true)
  })

  it('sends only the new name and shows the list read afterwards', async () => {
    const laptop = staffLaptop([ANNA], [{ ...ANNA, name: 'Annika' }])
    const staff = useAdminStaffStore()
    await staff.load()

    const result = await staff.rename('staff-anna', 'Annika')

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(laptop.writes().map((call) => [call.method, call.url, call.body])).toEqual([
      ['PUT', '/api/admin/staff-members/staff-anna', { name: 'Annika' }],
    ])
    expect(staff.staffMembers).toEqual([{ ...ANNA, name: 'Annika' }])
  })

  it('returns the refusal of a rename and keeps the list', async () => {
    staffLaptop([ANNA]).answers('PUT', /./, refusal('errors.staffMemberNameTaken'))
    const staff = useAdminStaffStore()
    await staff.load()

    const result = await staff.rename('staff-anna', 'Ben')

    expect(result.kind).toBe('failed')
    expect(staff.staffMembers).toEqual([ANNA])
  })

  it('takes a waiter off on the deactivate route and shows the list read afterwards', async () => {
    const laptop = staffLaptop([ANNA], [{ ...ANNA, isActive: false }])
    const staff = useAdminStaffStore()
    await staff.load()

    await staff.setActive('staff-anna', false)

    expect(laptop.writes().map((call) => call.url)).toEqual([
      '/api/admin/staff-members/staff-anna/deactivate',
    ])
    expect(staff.staffMembers).toEqual([{ ...ANNA, isActive: false }])
  })

  it('puts a waiter back on the activate route', async () => {
    const laptop = staffLaptop([{ ...BEN, isActive: false }], [BEN])
    const staff = useAdminStaffStore()
    await staff.load()

    await staff.setActive('staff-ben', true)

    expect(laptop.writes().map((call) => call.url)).toEqual([
      '/api/admin/staff-members/staff-ben/activate',
    ])
    expect(staff.staffMembers).toEqual([BEN])
  })
})

describe('the waiter list while its screen is open', () => {
  it('reads the list again when a waiter finishes setting up a phone', async () => {
    const laptop = staffLaptop([BEN], [{ ...BEN, hasDevice: true }])
    const staff = useAdminStaffStore()
    staff.listen()
    await useConnectionStore().connect({})
    await vi.waitFor(() => expect(staff.staffMembers).toEqual([BEN]))
    laptop.forgetCalls()

    fireHubEvent('EnrolmentCompleted', { staffMemberName: 'Ben', stationName: null })

    await vi.waitFor(() => expect(staff.staffMembers).toEqual([{ ...BEN, hasDevice: true }]))
    expect(laptop.urls()).toEqual(['/api/admin/staff-members'])
  })

  it('reads the list again when the laptop says the configuration changed', async () => {
    const laptop = staffLaptop([ANNA], [ANNA, BEN])
    const staff = useAdminStaffStore()
    staff.listen()
    await useConnectionStore().connect({})
    await vi.waitFor(() => expect(staff.staffMembers).toEqual([ANNA]))
    laptop.forgetCalls()

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(staff.staffMembers).toEqual([ANNA, BEN]))
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const laptop = staffLaptop([ANNA])
    const stopListening = useAdminStaffStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})
