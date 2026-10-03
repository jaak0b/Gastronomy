import { expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import StationsList from '../../../../src/admin/components/stations/StationsList.vue'
import { testPlugins } from '../../../support/plugins'
import { pressInDialog } from '../../../support/dom'
import { stubLaptop, answer, refusal } from '../../../support/laptop'
import { anAdminStation } from '../../../support/wireViews'

export const STATION_ID = '11111111-1111-1111-1111-111111111111'

export const ONE_STATION = { stations: [anAdminStation({ stationId: STATION_ID })] }

export function refuseDeactivationWith(messageKey: string, parameters: Record<string, unknown>) {
  stubLaptop()
    .answersEverythingElse(answer(ONE_STATION))
    .answers('POST', /./, refusal(messageKey, { parameters }))
}

export function mountList() {
  return mount(StationsList, { global: { plugins: testPlugins() }, attachTo: document.body })
}

export async function deactivateFirstStation(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
  await list.get('[data-test="deactivate"]').trigger('click')
  await pressInDialog('[data-test="confirm"]')
  await vi.waitFor(() => expect(list.find('[data-test="refusal"]').exists()).toBe(true))
}

export const ONE_STATION_SWITCHED_OFF = {
  stations: [anAdminStation({ stationId: STATION_ID, isActive: false })],
}
