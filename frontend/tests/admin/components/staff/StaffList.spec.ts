import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import StaffList from '../../../../src/admin/components/staff/StaffList.vue'
import { useConnectionStore } from '../../../../src/shared/stores/connection'
import { testPlugins } from '../../../support/plugins'
import { pressInDialog, waitForDialog, typeInto, clickOn } from '../../../support/dom'
import { stubLaptop, answer, refusal, type StubbedLaptop } from '../../../support/laptop'

const STAFF_MEMBER_ID = '33333333-3333-3333-3333-333333333333'

const ONE_STAFF_MEMBER = {
  staffMembers: [
    {
      staffMemberId: STAFF_MEMBER_ID,
      name: 'Anna',
      isActive: true,
      hasDevice: true,
    },
  ],
}

function staffLaptop(revokeStatus = 200): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer(ONE_STAFF_MEMBER))
    .answers(
      'ANY',
      /\/revoke-device$/,
      answer({ code: 'NotFound', messageKey: 'admin.personHasNoPhone' }, revokeStatus),
    )
}

function mountList() {
  return mount(StaffList, { global: { plugins: testPlugins() }, attachTo: document.body })
}

async function firstStaffMember(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('[data-test="staff-row"]').exists()).toBe(true))
}

const OFF_THE_LIST = {
  staffMembers: [{ ...ONE_STAFF_MEMBER.staffMembers[0], isActive: false }],
}

function staffLaptopListing(staffMembers: unknown): StubbedLaptop {
  return stubLaptop().answersEverythingElse(answer(staffMembers))
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
    const laptop = staffLaptopListing(ONE_STAFF_MEMBER)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="deactivate"]').trigger('click')

    await waitForDialog()

    expect(laptop.urls().some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('says that the phone is signed out', async () => {
    staffLaptopListing(ONE_STAFF_MEMBER)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="deactivate"]').trigger('click')

    await waitForDialog()

    expect(document.querySelector('[data-test="confirm-body"]')!.textContent).toContain(
      'Das Telefon des Kellners wird abgemeldet',
    )
  })

  it('takes them off the list once the question is answered with yes', async () => {
    const laptop = staffLaptopListing(ONE_STAFF_MEMBER)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="deactivate"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/staff-members/${STAFF_MEMBER_ID}/deactivate`),
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
    staffLaptopListing(OFF_THE_LIST)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="show-deactivated"]').exists()).toBe(true))

    expect(list.find('[data-test="staff-row"]').exists()).toBe(false)
  })

  it('says so on their row once it is shown', async () => {
    staffLaptopListing(OFF_THE_LIST)

    const list = mountList()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await firstStaffMember(list)

    expect(list.get('[data-test="deactivated"]').text()).toBe('Deaktiviert')
  })

  it('puts them back on the list without asking a question first', async () => {
    const laptop = staffLaptopListing(OFF_THE_LIST)

    const list = mountList()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await firstStaffMember(list)
    await list.get('[data-test="reactivate"]').trigger('click')

    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/staff-members/${STAFF_MEMBER_ID}/activate`),
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

  function stubInvitationFor(staffMember: unknown): StubbedLaptop {
    return stubLaptop()
      .answersEverythingElse(answer(ONE_STAFF_MEMBER))
      .answers(
        'ANY',
        (call) => call.url.includes('/invitations'),
        answer({
          invitationId: 'invitation-1',
          qrUrl: 'http://192.168.0.22:5000/j/CODE',
          expiresAtUtc: '2026-08-27T20:00:00Z',
          staffMember,
          station: null,
        }),
      )
  }

  it('sits inside the row of the staff member it belongs to', async () => {
    stubInvitationFor({ id: STAFF_MEMBER_ID, name: 'Anna' })

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="new-code"]').trigger('click')

    await vi.waitFor(() =>
      expect(list.get('[data-test="staff-row"]').find('[data-test="invitation-panel"]').exists()).toBe(true),
    )
  })

  it('sits on its own when it belongs to nobody yet', async () => {
    stubInvitationFor(null)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="new-staff-member"]').trigger('click')

    await vi.waitFor(() => expect(list.find('[data-test="invitation-panel"]').exists()).toBe(true))
    expect(list.get('[data-test="staff-row"]').find('[data-test="invitation-panel"]').exists()).toBe(false)
  })
})

describe('adding somebody new to the waiter list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks the laptop for a QR code that names nobody, because the waiter types their own name', async () => {
    const laptop = stubLaptop()
      .answersEverythingElse(answer(ONE_STAFF_MEMBER))
      .answers(
        'ANY',
        /\/invitations$/,
        answer(
          {
            invitationId: 'invitation-1',
            qrUrl: 'http://192.168.0.22:5000/j/CODE',
            expiresAtUtc: '2026-08-27T20:00:00Z',
            staffMember: null,
            station: null,
          },
          201,
        ),
      )
      .answers('GET', /\/qr\.svg/, () => new Response('<svg></svg>', {
        status: 200,
        headers: { 'Content-Type': 'image/svg+xml' },
      }))

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="new-staff-member"]').trigger('click')

    await vi.waitFor(() =>
      expect(laptop.calls).toContainEqual({
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
    stubLaptop()
      .answersEverythingElse(answer(ONE_STAFF_MEMBER))
      .answers(
        'PUT',
        /./,
        refusal('errors.admin.staff.nameMissing', { status: 400, code: 'ValidationFailed' }),
      )
      .answers(
        'ANY',
        /\/invitations$/,
        refusal('errors.enrolment.atMostOneOwner', { status: 400, code: 'ValidationFailed' }),
      )

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="new-code"]').trigger('click')
    await vi.waitFor(() => expect(list.find('[data-test="refusal"]').exists()).toBe(true))
    await list.get('[data-test="rename"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="staff-name-field"]')).not.toBeNull())
    typeInto('[data-test="staff-name-field"] input', 'Anna B')
    await flushPromises()
    await clickOn('[data-test="form-save"]')

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="refusal"]')?.textContent?.trim()).toBe(
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
    const laptop = staffLaptopListing(ONE_STAFF_MEMBER)
    const list = mountList()
    await firstStaffMember(list)

    list.unmount()
    laptop.forgetCalls()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
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
    staffLaptop()

    const list = mountList()
    await firstStaffMember(list)

    expect(list.find('[data-test="no-phone"]').exists()).toBe(false)
  })

  it('is deactivated at their own address', async () => {
    const laptop = staffLaptop()

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="deactivate"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/staff-members/${STAFF_MEMBER_ID}/deactivate`),
    )
  })

})

describe('a staff member who already has a phone', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('keeps the name and the buttons on one line', async () => {
    staffLaptop()

    const list = mountList()
    await firstStaffMember(list)

    const row = list.get('[data-test="staff-row-line"]')

    expect(row.find('[data-test="name"]').exists()).toBe(true)
    expect(row.find('[data-test="new-code"]').exists()).toBe(true)
    expect(row.find('[data-test="rename"]').exists()).toBe(true)
    expect(row.find('[data-test="deactivate"]').exists()).toBe(true)
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
    staffLaptopListing(ONE_STAFF_MEMBER)

    const list = mountList()
    await firstStaffMember(list)
    await list.get('[data-test="rename"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="staff-name-field"]')).not.toBeNull())

    expect(
      (document.querySelector('[data-test="staff-name-field"] input') as HTMLInputElement).getAttribute(
        'maxlength',
      ),
    ).toBe('40')
  })
})
