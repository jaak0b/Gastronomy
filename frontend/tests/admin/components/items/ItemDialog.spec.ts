import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { VSelect } from 'vuetify/components'
import { createPinia, setActivePinia } from 'pinia'
import ItemDialog from '../../../../src/admin/components/items/ItemDialog.vue'
import CategoryDialog from '../../../../src/admin/components/categories/CategoryDialog.vue'
import type { AdminCategory } from '../../../../src/shared/api/apiTypes'
import { useAdminCategoriesStore } from '../../../../src/admin/stores/categories'
import type { AdminItem } from '../../../../src/shared/api/apiTypes'
import { testPlugins } from '../../../support/plugins'

const FOOD_ID = '33333333-3333-3333-3333-333333333333'
const DRINKS_ID = '44444444-4444-4444-4444-444444444444'
const DESSERT_ID = '55555555-5555-5555-5555-555555555555'

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
  sortOrder: 1,
  isActive: true,
  productionMinutes: 15,
  isQueueIndependent: false,
  atTheFestival: { priceCents: 350, isAvailable: true, stationIds: ['station-kueche'] },
}

function mountDialog(item: AdminItem | null = null, locale: 'de' | 'en' = 'de'): VueWrapper {
  return mount(ItemDialog, {
    props: { item, errorText: null },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

function field(selector: string): HTMLInputElement {
  return document.querySelector(`${selector} input, input${selector}`) as HTMLInputElement
}

function typeInto(selector: string, value: string): void {
  const input = field(selector)
  input.value = value
  input.dispatchEvent(new Event('input'))
}

async function pressSave(dialog: VueWrapper): Promise<void> {
  ;(document.querySelector('.form-save') as HTMLElement).click()
  await dialog.vm.$nextTick()
}

function pressEnterInTheMinutes(dialog: VueWrapper): void {
  field('.production-minutes-field').dispatchEvent(
    new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }),
  )
}

function knownCategories(): void {
  useAdminCategoriesStore().categories = [...CATEGORIES]
}

function stubTheLaptop(): void {
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

describe('the item dialog', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubTheLaptop()
    knownCategories()
  })

  it('is headed as a new item when there is none yet', async () => {
    mountDialog()

    await vi.waitFor(() =>
      expect(document.querySelector('.form-dialog-title')?.textContent).toContain('Neuer Artikel'),
    )
  })

  it('is headed as an edit when an item is being changed', async () => {
    mountDialog(BRATWURST)

    await vi.waitFor(() =>
      expect(document.querySelector('.form-dialog-title')?.textContent).toContain(
        'Artikel bearbeiten',
      ),
    )
  })

  it('starts with the name the item already has', async () => {
    mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.item-name-field')).not.toBeNull())

    expect(field('.item-name-field').value).toBe('Bratwurst')
  })

  it('sends the whole item as the admin sees it', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.form-save')).not.toBeNull())
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toEqual({
      itemId: 'item-1',
      name: 'Bratwurst',
      categoryId: FOOD_ID,
      sortOrder: 1,
      productionMinutes: 15,
      isQueueIndependent: false,
    })
  })

  it('leaves the item alone when the admin cancels', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.form-cancel')).not.toBeNull())
    ;(document.querySelector('.form-cancel') as HTMLElement).click()
    await dialog.vm.$nextTick()

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('save')).toBeUndefined()
  })
})

describe('the preparation time field', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubTheLaptop()
    knownCategories()
  })

  it('names the field for the preparation time', async () => {
    mountDialog()

    await vi.waitFor(() =>
      expect(document.querySelector('.production-minutes-field label')).not.toBeNull(),
    )

    expect(document.querySelector('.production-minutes-field label')?.textContent).toBe(
      'Zubereitungszeit in Minuten',
    )
  })

  it('shows the minutes of an item that already has them', async () => {
    mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())

    expect(field('.production-minutes-field').value).toBe('15')
  })

  it('shows half a minute the way German writes it', async () => {
    mountDialog({ ...BRATWURST, productionMinutes: 1.5 })

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())

    expect(field('.production-minutes-field').value).toBe('1,5')
  })

  it('shows half a minute the way English writes it', async () => {
    mountDialog({ ...BRATWURST, productionMinutes: 1.5 }, 'en')

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())

    expect(field('.production-minutes-field').value).toBe('1.5')
  })

  it('stays empty for an item that is handed over right away', async () => {
    mountDialog({ ...BRATWURST, productionMinutes: null })

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())

    expect(field('.production-minutes-field').value).toBe('')
  })

  it('sends the minutes that were typed', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())
    typeInto('.production-minutes-field', '20')
    await dialog.vm.$nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 20 })
  })

  it('sends half a minute written with a comma, the way German writes it', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())
    typeInto('.production-minutes-field', '1,5')
    await dialog.vm.$nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 1.5 })
  })

  it('sends half a minute written with a dot, the way English writes it', async () => {
    const dialog = mountDialog(BRATWURST, 'en')

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())
    typeInto('.production-minutes-field', '1.5')
    await dialog.vm.$nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 1.5 })
  })

  it('sends no preparation time when the field is left empty', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())
    typeInto('.production-minutes-field', '')
    await dialog.vm.$nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: null })
  })

  it('saves the highest allowed time when more is typed and Enter is pressed', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())
    const input = field('.production-minutes-field')
    input.focus()
    typeInto('.production-minutes-field', '601')
    await dialog.vm.$nextTick()
    pressEnterInTheMinutes(dialog)
    await dialog.vm.$nextTick()

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 600 })
  })

  it('does not save on Enter while the name is missing', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.production-minutes-field')).not.toBeNull())
    field('.production-minutes-field').focus()

    typeInto('.item-name-field', '')
    await dialog.vm.$nextTick()
    typeInto('.production-minutes-field', '10')
    await dialog.vm.$nextTick()
    pressEnterInTheMinutes(dialog)
    await dialog.vm.$nextTick()

    expect(dialog.emitted('save')).toBeUndefined()
  })
})

describe('the independent preparation choice', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubTheLaptop()
    knownCategories()
  })

  it('shows the choice of an item and saves it', async () => {
    const dialog = mountDialog({ ...BRATWURST, isQueueIndependent: true })

    await vi.waitFor(() =>
      expect(document.querySelector('.queue-independent-checkbox')).not.toBeNull(),
    )

    expect(field('.queue-independent-checkbox').checked).toBe(true)

    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ isQueueIndependent: true })
  })
})

describe('the category field', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubTheLaptop()
    knownCategories()
  })

  it('offers the categories of the laptop in the order they were given', async () => {
    const dialog = mountDialog()

    await vi.waitFor(() => expect(document.querySelector('.category-field')).not.toBeNull())

    expect(
      dialog.getComponent(VSelect).props('items').map((category: AdminCategory) => category.name),
    ).toEqual(['Speisen', 'Getränke'])
  })

  it('starts on the category the item already belongs to', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.category-field')).not.toBeNull())

    expect(dialog.getComponent(VSelect).props('modelValue')).toBe(FOOD_ID)
  })

  it('sends the category that was picked', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.category-field')).not.toBeNull())
    dialog.getComponent(VSelect).vm.$emit('update:modelValue', DRINKS_ID)
    await dialog.vm.$nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ categoryId: DRINKS_ID })
  })

  it('names the way out when the laptop holds no category at all', async () => {
    useAdminCategoriesStore().categories = []
    mountDialog()

    await vi.waitFor(() => expect(document.querySelector('.category-field')).not.toBeNull())

    ;(document.querySelector('.category-field .v-field') as HTMLElement).dispatchEvent(
      new MouseEvent('mousedown', { bubbles: true }),
    )

    await vi.waitFor(() =>
      expect(document.body.textContent).toContain(
        'Klicken Sie auf "Neue Kategorie". Es gibt noch keine Kategorie, die Sie auswählen können.',
      ),
    )
  })

  it('asks for a category rather than saving an item without one', async () => {
    const dialog = mountDialog()

    await vi.waitFor(() => expect(document.querySelector('.item-name-field')).not.toBeNull())
    typeInto('.item-name-field', 'Pommes')
    await dialog.vm.$nextTick()
    await pressSave(dialog)

    expect(document.querySelector('.category-field .v-messages')?.textContent).toBe(
      'Wählen Sie eine Kategorie aus, bevor Sie speichern.',
    )
    expect(dialog.emitted('save')).toBeUndefined()
  })

  it('drops the question as soon as the admin picks a category', async () => {
    const dialog = mountDialog()

    await vi.waitFor(() => expect(document.querySelector('.item-name-field')).not.toBeNull())
    typeInto('.item-name-field', 'Pommes')
    await dialog.vm.$nextTick()
    await pressSave(dialog)

    dialog.getComponent(VSelect).vm.$emit('update:modelValue', DRINKS_ID)
    await dialog.vm.$nextTick()

    expect(document.querySelector('.category-field .v-messages')?.textContent).toBe('')
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
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.new-category')).not.toBeNull())

    expect(dialog.findComponent(CategoryDialog).exists()).toBe(false)

    ;(document.querySelector('.new-category') as HTMLElement).click()
    await dialog.vm.$nextTick()

    expect(dialog.findComponent(CategoryDialog).exists()).toBe(true)
  })

  it('picks the category the laptop created, so the item lands in it', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.new-category')).not.toBeNull())
    ;(document.querySelector('.new-category') as HTMLElement).click()
    await dialog.vm.$nextTick()
    dialog
      .findComponent(CategoryDialog)
      .vm.$emit('save', { name: 'Nachtisch', colourHex: '#6D4C41' })

    await vi.waitFor(() =>
      expect(dialog.getComponent(VSelect).props('modelValue')).toBe(DESSERT_ID),
    )
  })

  it('closes the dialog once the category is created', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('.new-category')).not.toBeNull())
    ;(document.querySelector('.new-category') as HTMLElement).click()
    await dialog.vm.$nextTick()
    dialog
      .findComponent(CategoryDialog)
      .vm.$emit('save', { name: 'Nachtisch', colourHex: '#6D4C41' })

    await vi.waitFor(() => expect(dialog.findComponent(CategoryDialog).exists()).toBe(false))
  })
})

describe('the length of an article name', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubTheLaptop()
    knownCategories()
  })

  it('stops where the laptop stops storing it', async () => {
    mountDialog()

    await vi.waitFor(() => expect(document.querySelector('.item-name-field')).not.toBeNull())

    expect(field('.item-name-field').getAttribute('maxlength')).toBe('200')
  })
})
