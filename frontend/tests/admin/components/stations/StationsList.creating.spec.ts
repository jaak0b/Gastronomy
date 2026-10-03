import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { typeInto, clickOn } from '../../../support/dom'
import { stubLaptop, answer, type StubbedLaptop } from '../../../support/laptop'
import { anAdminStation } from '../../../support/wireViews'
import { STATION_ID, ONE_STATION, mountList } from './stationsListFixture'

describe('a new station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('is shown in the list right away even when the answer to the list is still the old one', async () => {
    stubLaptop()
      .answersEverythingElse(answer(ONE_STATION))
      .answers(
        'POST',
        /./,
        answer(
          anAdminStation({
            stationId: 'station-neu',
            name: 'Zelt',
            sortOrder: 2,
            hasDevice: false,
            isAtAnyFestival: false,
          }),
          201,
        ),
      )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="new-station"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="station-name-field"]')).not.toBeNull())
    typeInto('[data-test="station-name-field"] input', 'Zelt')
    await flushPromises()
    await clickOn('[data-test="form-save"]')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
    expect(list.findAll('[data-test="station-row"] [data-test="name"]').map((row) => row.text())).toContain('Zelt')
  })
})

describe('setting up the tablet of a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  function stubInvitation(): StubbedLaptop {
    return stubLaptop()
      .answersEverythingElse(answer(ONE_STATION))
      .answers(
        'ANY',
        /\/invitations$/,
        answer(
          {
            invitationId: 'invitation-1',
            qrUrl: 'http://192.168.0.22:5000/j/CODE',
            expiresAtUtc: '2026-08-27T20:00:00Z',
            staffMember: null,
            station: { id: STATION_ID, name: 'Küche' },
          },
          201,
        ),
      )
      .answers(
        'GET',
        /\/qr\.svg/,
        () =>
          new Response('<svg></svg>', {
            status: 200,
            headers: { 'Content-Type': 'image/svg+xml' },
          }),
      )
  }

  it('asks the laptop for a code that belongs to that station', async () => {
    const laptop = stubInvitation()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="set-up-device"]').trigger('click')

    await vi.waitFor(() => expect(laptop.writtenBodies()).toEqual([{ stationId: STATION_ID }]))
  })

  it('shows the same invitation panel the waiter list uses, inside the station row', async () => {
    stubInvitation()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="set-up-device"]').trigger('click')

    await vi.waitFor(() =>
      expect(list.get('[data-test="station-row"]').find('[data-test="invitation-panel"]').exists()).toBe(true),
    )
  })

  it('tells the admin to scan the code with the tablet of that station', async () => {
    stubInvitation()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="set-up-device"]').trigger('click')

    await vi.waitFor(() => expect(list.find('[data-test="instruction"]').exists()).toBe(true))
    expect(list.get('[data-test="instruction"]').text()).toBe(
      'Scannen Sie diesen QR-Code mit der Kamera des Tablets an der Ausgabestelle Küche.',
    )
  })
})

describe('the length of a station name', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('stops where the laptop stops storing it', async () => {
    stubLaptop().answersEverythingElse(answer(ONE_STATION))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="edit"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="station-name-field"]')).not.toBeNull())

    expect(
      (document.querySelector('[data-test="station-name-field"] input') as HTMLInputElement).getAttribute(
        'maxlength',
      ),
    ).toBe('40')
  })
})
