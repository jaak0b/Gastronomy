import { beforeEach, describe, expect, it, vi } from 'vitest'
import { VSelect } from 'vuetify/components'
import { createPinia, setActivePinia } from 'pinia'
import CategoryDialog from '../../../../src/admin/components/categories/CategoryDialog.vue'
import { AdminCategoryView } from '../../../../src/shared/api/generatedSchemas'
import { useAdminCategoriesStore } from '../../../../src/admin/stores/categories'
import { typeInto } from '../../../support/dom'
import { nextTick } from 'vue'
import { saveTheCategoryDialog } from '../../../support/formDialogs'
import { FOOD_ID, DRINKS_ID, DESSERT_ID, BRATWURST, mountDialog, pressSave, knownCategories, categoriesLaptop } from './itemDialogFixture'

describe('the category field', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    categoriesLaptop()
    knownCategories()
  })

  it('offers the categories of the laptop in the order they were given', async () => {
    const dialog = mountDialog()

    await vi.waitFor(() => expect(document.querySelector('[data-test="category-field"]')).not.toBeNull())

    expect(
      dialog.getComponent(VSelect).props('items').map((category: AdminCategoryView) => category.name),
    ).toEqual(['Speisen', 'Getränke'])
  })

  it('starts on the category the item already belongs to', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="category-field"]')).not.toBeNull())

    expect(dialog.getComponent(VSelect).props('modelValue')).toBe(FOOD_ID)
  })

  it('sends the category that was picked', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="category-field"]')).not.toBeNull())
    await dialog.getComponent(VSelect).setValue(DRINKS_ID)
    await nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ categoryId: DRINKS_ID })
  })

  it('names the way out when the laptop holds no category at all', async () => {
    useAdminCategoriesStore().categories = []
    mountDialog()

    await vi.waitFor(() => expect(document.querySelector('[data-test="category-field"]')).not.toBeNull())

    ;(document.querySelector('[data-test="category-field"] .v-field') as HTMLElement).dispatchEvent(
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

    await vi.waitFor(() => expect(document.querySelector('[data-test="item-name-field"]')).not.toBeNull())
    typeInto('[data-test="item-name-field"]', 'Pommes')
    await nextTick()
    await pressSave(dialog)

    expect(document.querySelector('[data-test="category-field"] .v-messages')?.textContent).toBe(
      'Wählen Sie eine Kategorie aus, bevor Sie speichern.',
    )
    expect(dialog.emitted('save')).toBeUndefined()
  })

  it('drops the question as soon as the admin picks a category', async () => {
    const dialog = mountDialog()

    await vi.waitFor(() => expect(document.querySelector('[data-test="item-name-field"]')).not.toBeNull())
    typeInto('[data-test="item-name-field"]', 'Pommes')
    await nextTick()
    await pressSave(dialog)

    await dialog.getComponent(VSelect).setValue(DRINKS_ID)
    await nextTick()

    expect(document.querySelector('[data-test="category-field"] .v-messages')?.textContent).toBe('')
  })
})

describe('creating a category while an item is being written', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    categoriesLaptop()
    knownCategories()
  })

  it('opens the same dialog the item list uses', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="new-category"]')).not.toBeNull())

    expect(dialog.findComponent(CategoryDialog).exists()).toBe(false)

    ;(document.querySelector('[data-test="new-category"]') as HTMLElement).click()
    await nextTick()

    expect(dialog.findComponent(CategoryDialog).exists()).toBe(true)
  })

  it('picks the category the laptop created, so the item lands in it', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="new-category"]')).not.toBeNull())
    ;(document.querySelector('[data-test="new-category"]') as HTMLElement).click()
    await nextTick()
    await saveTheCategoryDialog('Nachtisch', '#6d4c41')

    await vi.waitFor(() =>
      expect(dialog.getComponent(VSelect).props('modelValue')).toBe(DESSERT_ID),
    )
  })

  it('closes the dialog once the category is created', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="new-category"]')).not.toBeNull())
    ;(document.querySelector('[data-test="new-category"]') as HTMLElement).click()
    await nextTick()
    await saveTheCategoryDialog('Nachtisch', '#6d4c41')

    await vi.waitFor(() => expect(dialog.findComponent(CategoryDialog).exists()).toBe(false))
  })
})
