import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { expect, vi } from 'vitest'
import { nextTick } from 'vue'
import IngredientDialog from '../../../../src/admin/components/ingredients/IngredientDialog.vue'
import { useAdminUnitsStore } from '../../../../src/admin/stores/units'
import type { AdminIngredientView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { allOnScreen, clickOn, onScreen } from '../../../support/dom'
import { formDialogHolding } from '../../../support/formDialogs'
import { answer, emptyAnswer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../../support/laptop'
import { GRAM_UNIT, KISTE_UNIT, MILLILITRE_UNIT, PIECE_UNIT } from '../../../support/wireViews'

export const LISTED_UNITS = [PIECE_UNIT, GRAM_UNIT, MILLILITRE_UNIT, KISTE_UNIT]

export interface UnitsLaptopScenario {
  listed?: LaptopReply
  writes?: LaptopReply
}

export function unitsLaptop(scenario: UnitsLaptopScenario = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(scenario.writes ?? emptyAnswer(204))
    .answers('GET', '/api/admin/units', scenario.listed ?? answer({ units: LISTED_UNITS }))
}

export interface DialogMount {
  locale?: 'de' | 'en'
  errorText?: string | null
  loadsTheUnitsFirst?: boolean
}

export async function mountDialog(
  ingredient: AdminIngredientView | null = null,
  { locale = 'de', errorText = null, loadsTheUnitsFirst = true }: DialogMount = {},
): Promise<VueWrapper> {
  if (loadsTheUnitsFirst) {
    await useAdminUnitsStore().load()
  }
  const dialog = mount(IngredientDialog, {
    props: { ingredient, errorText },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
  await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
  return dialog
}

export function chipOf(unitId: string): string {
  return `[data-test="unit-chip"][data-test-id="${unitId}"]`
}

export function namesOnTheChips(): string[] {
  return allOnScreen('[data-test="unit-chip"] [data-test="unit-name"]').map((name) => name.textContent ?? '')
}

export function chosenUnitIds(): string[] {
  return allOnScreen('[data-test="unit-chip"].v-chip--selected').map((chip) => chip.dataset.testId ?? '')
}

function unitIdsOfChipsHolding(selector: string): string[] {
  return allOnScreen('[data-test="unit-chip"]')
    .filter((chip) => chip.querySelector(selector) !== null)
    .map((chip) => chip.dataset.testId ?? '')
}

export function unitIdsWithATrashButton(): string[] {
  return unitIdsOfChipsHolding('[data-test="delete-unit"]')
}

export function unitIdsOfferedForDeleting(): string[] {
  return unitIdsOfChipsHolding('[data-test="delete-unit"]:not([disabled])')
}

export async function hoverTheTrashButtonOf(unitId: string): Promise<void> {
  onScreen(`${chipOf(unitId)} [data-test="delete-unit-wrapper"]`).dispatchEvent(new MouseEvent('mouseenter'))
  await nextTick()
}

export function unitIdsOfferedForRenaming(): string[] {
  return unitIdsOfChipsHolding('[data-test="rename-unit"]')
}

export async function pressTheKeyOn(selector: string, key: 'Enter' | ' '): Promise<void> {
  const button = onScreen(selector)
  button.focus()
  const keydownWentThrough = button.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }))
  // jsdom runs no activation for a key; a browser clicks the focused button unless its keydown was cancelled.
  if (keydownWentThrough) {
    button.click()
  }
  await flushPromises()
}

export async function chooseUnit(unitId: string): Promise<void> {
  await clickOn(chipOf(unitId))
}

export async function openTheUnitFormToRename(unitId: string): Promise<void> {
  await clickOn(`${chipOf(unitId)} [data-test="rename-unit"]`)
  await vi.waitFor(() => expect(document.querySelector('[data-test="unit-name-field"]')).not.toBeNull())
}

export async function openTheUnitFormToCreate(): Promise<void> {
  await clickOn('[data-test="new-unit"]')
  await vi.waitFor(() => expect(document.querySelector('[data-test="unit-name-field"]')).not.toBeNull())
}

export function theUnitForm(): HTMLElement {
  return formDialogHolding('[data-test="unit-name-field"]')
}

export function unitFormIsOpen(): boolean {
  return document.querySelector('[data-test="unit-name-field"]') !== null
}

export function saveIsDisabled(): boolean {
  const ingredientForm = formDialogHolding('[data-test="ingredient-name-field"]')
  return (onScreen('[data-test="form-save"]', ingredientForm) as HTMLButtonElement).disabled
}
