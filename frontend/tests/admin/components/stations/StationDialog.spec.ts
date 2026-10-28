import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import StationDialog from '../../../../src/admin/components/stations/StationDialog.vue'
import type { AdminStationView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { onScreen } from '../../../support/dom'
import { anAdminStation } from '../../../support/wireViews'

const KITCHEN = anAdminStation({ stationId: 'station-kueche', name: 'Küche' })

async function mountDialog(station: AdminStationView | null, locale: 'de' | 'en' = 'de') {
  const dialog = mount(StationDialog, {
    props: { station, errorText: null },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
  await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
  return dialog
}

function nameFieldLabel(): string {
  return onScreen('[data-test="station-name-field"] .v-label').textContent?.trim() ?? ''
}

describe('the form for a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('names its field the name of the station while a station is being changed', async () => {
    await mountDialog(KITCHEN)

    expect(nameFieldLabel()).toBe('Name der Ausgabestelle')
  })

  it('names its field the same way for a new station', async () => {
    await mountDialog(null)

    expect(nameFieldLabel()).toBe('Name der Ausgabestelle')
  })

  it('names its field in English on an English laptop', async () => {
    await mountDialog(KITCHEN, 'en')

    expect(nameFieldLabel()).toBe('Station name')
  })
})
