import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import ItemForm from '../../../src/components/admin/items/ItemForm.vue'
import type { AdminItem } from '../../../src/stores/admin/items'
import type { AdminLocation } from '../../../src/stores/admin/locations'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const LOCATIONS: AdminLocation[] = [
  { id: 'location-kueche', name: 'Küche', sortOrder: 1, slipLanguage: 'de', isActive: true },
]

const BRATWURST: AdminItem = {
  id: 'item-1',
  name: 'Bratwurst',
  categoryName: 'Essen',
  priceCents: 350,
  sortOrder: 1,
  isAvailable: true,
  locationIds: ['location-kueche'],
}

function mountForm(item: AdminItem | null = null) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(ItemForm, {
    props: { item, locations: LOCATIONS, errorKey: null },
    global: { plugins: [i18n] },
  })
}

describe('the price field', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('asks for euros rather than cents', () => {
    const form = mountForm()

    expect(form.get('.price-field span').text()).toBe('Preis in Euro')
  })

  it('shows an existing price in euros', () => {
    const form = mountForm(BRATWURST)

    expect((form.get('.price-field input').element as HTMLInputElement).value).toBe('3,50')
  })

  it('sends a price typed with a comma as cents', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.price-field input').setValue('4,20')
    await form.get('form').trigger('submit')

    expect(form.emitted('save')?.[0]?.[0]).toMatchObject({ priceCents: 420 })
  })

  it('sends a price typed with a dot as cents', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.price-field input').setValue('4.20')
    await form.get('form').trigger('submit')

    expect(form.emitted('save')?.[0]?.[0]).toMatchObject({ priceCents: 420 })
  })

  it('sends a whole euro price as cents', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.price-field input').setValue('5')
    await form.get('form').trigger('submit')

    expect(form.emitted('save')?.[0]?.[0]).toMatchObject({ priceCents: 500 })
  })

  it('says how to write a price rather than saving something it could not read', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.price-field input').setValue('drei Euro')
    await form.get('form').trigger('submit')

    expect(form.get('.price-error').text()).toBe(
      'Tragen Sie den Preis in Euro ein, zum Beispiel 3,50.',
    )
  })

  it('sends nothing while the price cannot be read', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.price-field input').setValue('drei Euro')
    await form.get('form').trigger('submit')

    expect(form.emitted('save')).toBeUndefined()
  })
})
