import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import FestivalPage from '../../../../src/admin/components/festivals/FestivalPage.vue'
import { useConnectionStore } from '../../../../src/shared/stores/connection'
import { testPlugins } from '../../../support/plugins'
import { pressInDialog, typeInto, clickOn } from '../../../support/dom'
import { stubLaptop, answer, refusal } from '../../../support/laptop'
import { aFestival, anAdminStation } from '../../../support/wireViews'
import { STATION_ID, ONE_STATION, refuseDeactivationWith, mountList, deactivateFirstStation } from './stationsListFixture'

describe('a rename the laptop refuses', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  function refuseTheRename() {
    stubLaptop()
      .answersEverythingElse(answer(ONE_STATION))
      .answers(
        'PUT',
        /./,
        refusal('errors.admin.stations.nameMissing', { status: 400, code: 'ValidationFailed' }),
      )
  }

  async function renameFirstStation(list: ReturnType<typeof mountList>) {
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="edit"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="station-name-field"]')).not.toBeNull())
    typeInto('[data-test="station-name-field"] input', 'Küche hinten')
    await flushPromises()
    await clickOn('[data-test="form-save"]')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"] [data-test="refusal"]')).not.toBeNull())
  }

  it('says why the new name was not taken', async () => {
    refuseTheRename()

    const list = mountList()
    await renameFirstStation(list)

    expect(document.querySelector('[data-test="form-dialog"] [data-test="refusal"]')?.textContent?.trim()).toBe(
      'Geben Sie der Ausgabestelle einen Namen, bevor Sie sie speichern.',
    )
  })

  it('keeps the name the admin typed on screen, so it is not typed again', async () => {
    refuseTheRename()

    const list = mountList()
    await renameFirstStation(list)

    expect((document.querySelector('[data-test="station-name-field"] input') as HTMLInputElement).value).toBe(
      'Küche hinten',
    )
  })
})

describe('two refusals one after the other', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('shows the newest one, not the one the admin has already read', async () => {
    stubLaptop()
      .answersEverythingElse(answer(ONE_STATION))
      .answers(
        'POST',
        /./,
        refusal('errors.admin.stations.hasUnfinishedItems', { parameters: { count: 3 } }),
      )
      .answers(
        'ANY',
        /\/invitations$/,
        refusal('errors.enrolment.atMostOneOwner', { status: 400, code: 'ValidationFailed' }),
      )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="set-up-device"]').trigger('click')
    await vi.waitFor(() => expect(list.find('[data-test="refusal"]').exists()).toBe(true))
    await list.get('[data-test="deactivate"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(list.get('[data-test="refusal"]').text()).toBe(
        'Solange ein Fest aktiv ist, kann eine Ausgabestelle mit offenen Bestellungen nicht abgeschaltet werden.',
      ),
    )
  })
})

describe('the station screen the admin has left', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('no longer reloads the list when the laptop reports a change', async () => {
    const laptop = stubLaptop().answersEverythingElse(answer(ONE_STATION))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    list.unmount()
    laptop.forgetCalls()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})

describe('a refusal the admin has moved on from', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('is dropped once the admin opens a station to edit it', async () => {
    refuseDeactivationWith('errors.admin.stations.hasUnfinishedItems', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    await list.get('[data-test="edit"]').trigger('click')

    expect(list.find('[data-test="refusal"]').exists()).toBe(false)
  })

  it('is dropped once the admin starts a new station', async () => {
    refuseDeactivationWith('errors.admin.stations.hasUnfinishedItems', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    await list.get('[data-test="new-station"]').trigger('click')

    expect(list.find('[data-test="refusal"]').exists()).toBe(false)
  })
})

describe('a refusal the admin has walked away from', () => {
  const FESTIVAL_ID = 'fest-1'

  const FESTIVAL = aFestival({ festivalId: FESTIVAL_ID, menuItemCount: 0 })

  const STATION_AT_THE_FESTIVAL = anAdminStation({ stationId: STATION_ID })

  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  function laptopRefusingEveryChange(): void {
    stubLaptop()
      .answersEverythingElse(answer({ stations: [STATION_AT_THE_FESTIVAL] }))
      .answers('GET', '/api/admin/items', answer({ items: [] }))
      .answers('GET', '/api/admin/categories', answer({ categories: [] }))
      .answers('GET', '/api/admin/festivals', answer({ festivals: [FESTIVAL] }))
      .answers(
        'ANY',
        (call) => call.method !== 'GET',
        refusal('errors.admin.festivals.stationHasOrdersAtTheFestival', {
          parameters: { count: '3' },
        }),
      )
  }

  function mountFestivalPage() {
    return mount(FestivalPage, {
      props: { festivalId: FESTIVAL_ID },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
  }

  it('does not follow the admin from a festival page to the stations page', async () => {
    laptopRefusingEveryChange()

    const festival = mountFestivalPage()
    await vi.waitFor(() => expect(festival.find('[data-test="remove-station"]').exists()).toBe(true))
    await festival.get('[data-test="remove-station"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')
    await vi.waitFor(() =>
      expect(festival.find('[data-test="festival-station-row"] [data-test="refusal"]').exists()).toBe(true),
    )

    festival.unmount()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))

    expect(list.find('[data-test="refusal"]').exists()).toBe(false)
  })

  it('does not follow the admin from the stations page to a festival page', async () => {
    laptopRefusingEveryChange()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')
    await vi.waitFor(() => expect(list.find('[data-test="refusal"]').exists()).toBe(true))

    list.unmount()
    const festival = mountFestivalPage()
    await vi.waitFor(() => expect(festival.find('[data-test="festival-station-row"]').exists()).toBe(true))

    expect(festival.find('[data-test="festival-stations"] [data-test="refusal"]').exists()).toBe(false)
  })

  it('does not keep a refused device setup when the admin leaves and returns', async () => {
    stubLaptop()
      .answersEverythingElse(answer({ stations: [STATION_AT_THE_FESTIVAL] }))
      .answers('ANY', /\/invitations$/, refusal('errors.enrolment.atMostOneOwner'))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="set-up-device"]').trigger('click')
    await vi.waitFor(() => expect(list.find('[data-test="refusal"]').exists()).toBe(true))

    list.unmount()
    const again = mountList()
    await vi.waitFor(() => expect(again.find('[data-test="station-row"]').exists()).toBe(true))

    expect(again.find('[data-test="refusal"]').exists()).toBe(false)
  })
})
