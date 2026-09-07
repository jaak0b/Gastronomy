import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { VSelect } from 'vuetify/components'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import ItemForm from '../../../src/components/admin/items/ItemForm.vue'
import CategoryDialog from '../../../src/components/admin/categories/CategoryDialog.vue'
import type { AdminCategory } from '../../../src/core/apiTypes'
import { useAdminCategoriesStore } from '../../../src/stores/admin/categories'
import type { AdminItem } from '../../../src/stores/admin/items'
import type { AdminStation } from '../../../src/stores/admin/stations'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const FOOD_ID = '33333333-3333-3333-3333-333333333333'
const DRINKS_ID = '44444444-4444-4444-4444-444444444444'
const DESSERT_ID = '55555555-5555-5555-5555-555555555555'

const STATIONS: AdminStation[] = [
  { stationId: 'station-kueche', name: 'Küche', sortOrder: 1, isActive: true, hasDevice: true },
]

const CATEGORIES: AdminCategory[] = [
  { categoryId: FOOD_ID, name: 'Speisen', colourHex: '#FFEB3B', sortOrder: 1, isActive: true },
  { categoryId: DRINKS_ID, name: 'Getränke', colourHex: '#C62828', sortOrder: 2, isActive: true },
]

const CREATED_CATEGORY: AdminCategory = {
  categoryId: DESSERT_ID,
  name: 'Nachtisch',
  colourHex: '#6D4C41',
  sortOrder: 3,
  isActive: true,
}

const BRATWURST: AdminItem = {
  itemId: 'item-1',
  name: 'Bratwurst',
  categoryId: FOOD_ID,
  priceCents: 350,
  sortOrder: 1,
  isActive: true,
  isAvailable: true,
  stationIds: ['station-kueche'],
  productionMinutes: 15,
}

function mountForm(item: AdminItem | null = null) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(ItemForm, {
    props: { item, stations: STATIONS, errorText: null },
    global: { plugins: [i18n] },
    attachTo: document.body,
  })
}

function knownCategories() {
  useAdminCategoriesStore().categories = [...CATEGORIES]
}

function stubTheLaptop() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      if (method === 'POST' && url === '/api/admin/categories') {
        return new Response(JSON.stringify(CREATED_CATEGORY), { status: 201 })
      }
      return new Response(JSON.stringify({ categories: [...CATEGORIES, CREATED_CATEGORY] }), {
        status: 200,
      })
    }),
  )
}

describe('the price field', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    stubTheLaptop()
    knownCategories()
  })

  it('asks for euros rather than cents', () => {
    const form = mountForm()

    expect(form.get('.price-field label').text()).toBe('Preis in Euro')
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

    expect(form.get('.price-field .v-messages').text()).toBe(
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

describe('the preparation time field', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    stubTheLaptop()
    knownCategories()
  })

  it('asks for whole minutes', () => {
    const form = mountForm()

    expect(form.get('.production-minutes-field label').text()).toBe('Zubereitungszeit in Minuten')
  })

  it('shows the minutes of an item that already has them', () => {
    const form = mountForm(BRATWURST)

    expect((form.get('.production-minutes-field input').element as HTMLInputElement).value).toBe(
      '15',
    )
  })

  it('stays empty for an item that is handed over right away', () => {
    const form = mountForm({ ...BRATWURST, productionMinutes: null })

    expect((form.get('.production-minutes-field input').element as HTMLInputElement).value).toBe('')
  })

  it('sends the minutes that were typed', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.production-minutes-field input').setValue('20')
    await form.get('form').trigger('submit')

    expect(form.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 20 })
  })

  it('sends no preparation time when the field is left empty', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.production-minutes-field input').setValue('')
    await form.get('form').trigger('submit')

    expect(form.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: null })
  })

  it('says what it accepts rather than saving a time it could not read', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.production-minutes-field input').setValue('601')
    await form.get('form').trigger('submit')

    expect(form.get('.production-minutes-field .v-messages').text()).toBe(
      'Tragen Sie ganze Minuten von 0 bis 600 ein.',
    )
    expect(form.emitted('save')).toBeUndefined()
  })
})

describe('the category field', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubTheLaptop()
    knownCategories()
  })

  it('offers the categories of the laptop in the order they were given', () => {
    const form = mountForm()

    expect(
      form.getComponent(VSelect).props('items').map((category: AdminCategory) => category.name),
    ).toEqual(['Speisen', 'Getränke'])
  })

  it('starts on the category the item already belongs to', () => {
    const form = mountForm(BRATWURST)

    expect(form.getComponent(VSelect).props('modelValue')).toBe(FOOD_ID)
  })

  it('sends the category that was picked', async () => {
    const form = mountForm(BRATWURST)

    form.getComponent(VSelect).vm.$emit('update:modelValue', DRINKS_ID)
    await form.vm.$nextTick()
    await form.get('form').trigger('submit')

    expect(form.emitted('save')?.[0]?.[0]).toMatchObject({ categoryId: DRINKS_ID })
  })

  it('names the way out when the laptop holds no category at all', async () => {
    useAdminCategoriesStore().categories = []
    const form = mountForm()

    await form.get('.category-field .v-field').trigger('mousedown')

    await vi.waitFor(() =>
      expect(document.body.textContent).toContain(
        'Klicken Sie auf "Neue Kategorie". Es gibt noch keine Kategorie, die Sie auswählen können.',
      ),
    )
  })

  it('asks for a category rather than saving an item without one', async () => {
    const form = mountForm()

    await form.get('form').trigger('submit')

    expect(form.get('.category-field .v-messages').text()).toBe(
      'Wählen Sie eine Kategorie aus, bevor Sie speichern.',
    )
    expect(form.emitted('save')).toBeUndefined()
  })

  it('drops the question as soon as the admin picks a category', async () => {
    const form = mountForm()
    await form.get('form').trigger('submit')

    form.getComponent(VSelect).vm.$emit('update:modelValue', DRINKS_ID)
    await form.vm.$nextTick()

    expect(form.get('.category-field .v-messages').text()).toBe('')
  })
})

describe('creating a category while an item is being written', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubTheLaptop()
    knownCategories()
  })

  it('opens the same dialog the item list uses', async () => {
    const form = mountForm(BRATWURST)

    expect(form.findComponent(CategoryDialog).exists()).toBe(false)

    await form.get('.new-category').trigger('click')

    expect(form.findComponent(CategoryDialog).exists()).toBe(true)
  })

  it('picks the category the laptop created, so the item lands in it', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.new-category').trigger('click')
    form
      .findComponent(CategoryDialog)
      .vm.$emit('save', { name: 'Nachtisch', colourHex: '#6D4C41' })

    await vi.waitFor(() =>
      expect(form.getComponent(VSelect).props('modelValue')).toBe(DESSERT_ID),
    )
  })

  it('closes the dialog once the category is created', async () => {
    const form = mountForm(BRATWURST)

    await form.get('.new-category').trigger('click')
    form
      .findComponent(CategoryDialog)
      .vm.$emit('save', { name: 'Nachtisch', colourHex: '#6D4C41' })

    await vi.waitFor(() => expect(form.findComponent(CategoryDialog).exists()).toBe(false))
  })
})

describe('the length of an article name', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    stubTheLaptop()
    knownCategories()
  })

  it('stops where the laptop stops storing it', () => {
    const form = mountForm()

    expect(form.get('.item-name-field input').attributes('maxlength')).toBe('200')
  })
})
