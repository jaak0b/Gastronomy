import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { VSelect } from 'vuetify/components'
import { clickOn, onScreen, typeInto } from './dom'

export function formDialogHolding(selector: string): HTMLElement {
  const dialog = onScreen(selector).closest<HTMLElement>('[data-test="form-dialog"]')
  if (dialog === null) {
    throw new Error(`no form dialog holds ${selector}`)
  }
  return dialog
}

export async function saveTheFormDialog(dialog: HTMLElement): Promise<void> {
  await flushPromises()
  await clickOn('[data-test="form-save"]', dialog)
}

export async function cancelTheFormDialog(dialog: HTMLElement): Promise<void> {
  await clickOn('[data-test="form-cancel"]', dialog)
}

export async function saveTheCategoryDialog(name: string, colourHex: string): Promise<void> {
  const dialog = formDialogHolding('[data-test="category-name-field"]')
  typeInto(onScreen('[data-test="category-name-field"]', dialog), name)
  typeInto(onScreen('[data-test="category-colour-field"]', dialog), colourHex)
  await saveTheFormDialog(dialog)
}

export async function saveTheItemDialog(
  itemDialog: VueWrapper,
  name: string,
  categoryId: string,
): Promise<void> {
  const dialog = formDialogHolding('[data-test="item-name-field"]')
  typeInto(onScreen('[data-test="item-name-field"]', dialog), name)
  await itemDialog.getComponent(VSelect).setValue(categoryId)
  await saveTheFormDialog(dialog)
}

export async function saveTheIngredientDialog(
  ingredientDialog: VueWrapper,
  name: string,
  unit: string,
): Promise<void> {
  const dialog = formDialogHolding('[data-test="ingredient-name-field"]')
  typeInto(onScreen('[data-test="ingredient-name-field"]', dialog), name)
  await ingredientDialog.getComponent(VSelect).setValue(unit)
  await saveTheFormDialog(dialog)
}

export async function saveTheAreaDialog(name: string): Promise<void> {
  const dialog = formDialogHolding('[data-test="area-name-field"]')
  typeInto(onScreen('[data-test="area-name-field"]', dialog), name)
  await saveTheFormDialog(dialog)
}

export async function saveTheTableDialog(name: string): Promise<void> {
  const dialog = formDialogHolding('[data-test="table-name-field"]')
  typeInto(onScreen('[data-test="table-name-field"]', dialog), name)
  await saveTheFormDialog(dialog)
}
