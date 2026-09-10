import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import MenuItemDialog from '../../../src/components/admin/items/MenuItemDialog.vue'
import type { AdminStation } from '../../../src/stores/admin/stations'
import { testPlugins } from '../../support/plugins'

const KITCHEN: AdminStation = {
  stationId: 'station-kueche',
  name: 'Küche',
  sortOrder: 1,
  isActive: true,
  hasDevice: true,
  isAtTheFestival: true,
}

function mountDialog(priceCents: number | null = null, stationIds: string[] = []) {
  return mount(MenuItemDialog, {
    props: {
      itemName: 'Bratwurst',
      stations: [KITCHEN],
      priceCents,
      stationIds,
      errorText: null,
    },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

function inDialog(selector: string): HTMLElement {
  return document.querySelector(selector) as HTMLElement
}

describe('putting an item on a festival menu', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('asks for the price in euros', () => {
    mountDialog()

    expect(inDialog('.price-field label').textContent).toBe('Preis in Euro')
  })

  it('shows the price and the stations the item already has here', () => {
    mountDialog(350, ['station-kueche'])

    expect((inDialog('.price-field input') as HTMLInputElement).value).toBe('3,50')
    expect(inDialog('.station-chip.is-selected').textContent?.trim()).toBe('Küche')
  })

  it('sends the price typed with a comma as cents', async () => {
    const dialog = mountDialog()
    const price = inDialog('.price-field input') as HTMLInputElement
    price.value = '4,20'
    price.dispatchEvent(new Event('input'))
    inDialog('.station-chip').click()
    await dialog.vm.$nextTick()

    inDialog('.confirm').click()
    await dialog.vm.$nextTick()

    expect(dialog.emitted('confirm')?.[0]?.[0]).toEqual({
      priceCents: 420,
      stationIds: ['station-kueche'],
    })
  })

  it('says how to write a price rather than sending something it could not read', async () => {
    const dialog = mountDialog()
    const price = inDialog('.price-field input') as HTMLInputElement
    price.value = 'drei Euro'
    price.dispatchEvent(new Event('input'))
    inDialog('.station-chip').click()
    await dialog.vm.$nextTick()

    inDialog('.confirm').click()
    await dialog.vm.$nextTick()

    expect(inDialog('.price-field .v-messages').textContent).toBe(
      'Tragen Sie den Preis in Euro ein, zum Beispiel 3,50.',
    )
    expect(dialog.emitted('confirm')).toBeUndefined()
  })

  it('asks for a station rather than sending an item nobody would prepare', async () => {
    const dialog = mountDialog()
    const price = inDialog('.price-field input') as HTMLInputElement
    price.value = '3,50'
    price.dispatchEvent(new Event('input'))
    await dialog.vm.$nextTick()

    inDialog('.confirm').click()
    await dialog.vm.$nextTick()

    expect(inDialog('.needs-a-station').textContent?.trim()).toBe(
      'Wählen Sie mindestens eine Ausgabestelle für den Artikel.',
    )
    expect(dialog.emitted('confirm')).toBeUndefined()
  })

  it('drops that question as soon as a station is chosen', async () => {
    const dialog = mountDialog()
    inDialog('.confirm').click()
    await dialog.vm.$nextTick()

    inDialog('.station-chip').click()
    await dialog.vm.$nextTick()

    expect(document.querySelector('.needs-a-station')).toBeNull()
  })

  it('sends nothing when the admin backs out', async () => {
    const dialog = mountDialog()

    inDialog('.cancel').click()
    await dialog.vm.$nextTick()

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('confirm')).toBeUndefined()
  })
})
