<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { adminErrorMessageForKey } from '../../core/adminErrorMessage'
import {
  ALL_ENTRY_UNITS,
  entryUnitsFor,
  ingredientUnitOf,
  type AmountEntryUnit,
  type ParsedAmountInput,
} from '../../core/ingredientAmounts'
import type { AdminIngredientView, AdminItemView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminIngredientsStore } from '../../stores/ingredients'
import { useAdminItemsStore } from '../../stores/items'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'
import IngredientAmountField from '../ingredients/IngredientAmountField.vue'

interface RecipeLine {
  ingredient: AdminIngredientView
  amount: number
}

type AddedIngredient =
  | { kind: 'existing'; ingredient: AdminIngredientView }
  | { kind: 'new'; name: string }
  | { kind: 'none' }

const AMOUNT_INVALID_KEY = 'errors.admin.ingredients.amountInvalid'

const props = defineProps<{ item: AdminItemView }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const ingredients = useAdminIngredientsStore()
const items = useAdminItemsStore()
const { refusal, refusalText, showRefusalOf } = useRefusalDisplay()
const chosenIngredient = ref<AdminIngredientView | string | null>(null)
const addedAmount = ref<ParsedAmountInput>({ kind: 'empty' })
const addedEntryUnit = ref<AmountEntryUnit>(ALL_ENTRY_UNITS[0])
const addRowVersion = ref(0)
const isAdding = ref(false)

const recipeLines = computed<RecipeLine[]>(() =>
  props.item.ingredients.flatMap((line) => {
    const ingredient = ingredients.findIngredientWithId(line.ingredientId)
    return ingredient === null ? [] : [{ ingredient, amount: line.amount }]
  }),
)

const offeredIngredients = computed(() => {
  const inTheRecipe = new Set(props.item.ingredients.map((line) => line.ingredientId))
  return ingredients.ingredients.filter(
    (ingredient) => ingredient.isActive && !inTheRecipe.has(ingredient.ingredientId),
  )
})

const addedIngredient = computed<AddedIngredient>(() => {
  const chosen = chosenIngredient.value
  if (chosen === null) {
    return { kind: 'none' }
  }
  if (typeof chosen !== 'string') {
    return { kind: 'existing', ingredient: chosen }
  }
  const named = offeredIngredients.value.find((ingredient) => ingredient.name === chosen)
  if (named !== undefined) {
    return { kind: 'existing', ingredient: named }
  }
  return chosen.trim().length === 0 ? { kind: 'none' } : { kind: 'new', name: chosen }
})

const addedEntryUnits = computed(() => {
  const added = addedIngredient.value
  switch (added.kind) {
    case 'existing':
      return entryUnitsFor(added.ingredient.unit)
    case 'new':
    case 'none':
      return ALL_ENTRY_UNITS
    default:
      return assertNever(added)
  }
})

const addIsPossible = computed(
  () =>
    addedIngredient.value.kind !== 'none' &&
    addedAmount.value.kind === 'amount' &&
    addedAmount.value.baseAmount > 0,
)

async function changeAmount(ingredientId: string, parsed: ParsedAmountInput): Promise<void> {
  refusal.value = null
  switch (parsed.kind) {
    case 'amount':
      showRefusalOf(await items.setIngredientAmount(props.item.itemId, ingredientId, parsed.baseAmount))
      return
    case 'empty':
    case 'unreadable':
      refusal.value = adminErrorMessageForKey(AMOUNT_INVALID_KEY)
      return
    default:
      assertNever(parsed)
  }
}

async function removeLine(ingredientId: string): Promise<void> {
  refusal.value = null
  showRefusalOf(await items.removeIngredient(props.item.itemId, ingredientId))
}

function noteAddedAmount(parsed: ParsedAmountInput, entryUnit: AmountEntryUnit): void {
  addedAmount.value = parsed
  addedEntryUnit.value = entryUnit
}

async function ingredientIdToAdd(added: AddedIngredient): Promise<string | null> {
  switch (added.kind) {
    case 'existing':
      return added.ingredient.ingredientId
    case 'new': {
      const created = await ingredients.create({
        name: added.name,
        unit: ingredientUnitOf(addedEntryUnit.value),
      })
      switch (created.kind) {
        case 'ok':
          chosenIngredient.value = created.value
          return created.value.ingredientId
        case 'failed':
          refusal.value = created.message
          return null
        default:
          return assertNever(created)
      }
    }
    case 'none':
      return null
    default:
      return assertNever(added)
  }
}

async function add(): Promise<void> {
  const amount = addedAmount.value
  if (amount.kind !== 'amount') {
    return
  }
  refusal.value = null
  isAdding.value = true
  const ingredientId = await ingredientIdToAdd(addedIngredient.value)
  if (ingredientId === null) {
    isAdding.value = false
    return
  }
  const saved = await items.setIngredientAmount(props.item.itemId, ingredientId, amount.baseAmount)
  isAdding.value = false
  if (!showRefusalOf(saved)) {
    chosenIngredient.value = null
    addedAmount.value = { kind: 'empty' }
    addRowVersion.value += 1
  }
}
</script>

<template>
  <BaseFormDialog
    :title="t('admin.ingredients.labels.recipeTitle', { item: item.name })"
    :error-text="refusalText"
    :cancel-label="t('admin.ingredients.actions.close')"
    :busy="isAdding"
    close-only
    @cancel="emit('close')"
  >
    <div class="recipe-lines mb-4">
      <div
        v-for="line in recipeLines"
        :key="line.ingredient.ingredientId"
        class="recipe-line d-flex align-center flex-wrap ga-2 py-1"
        data-test="recipe-line"
        :data-test-id="line.ingredient.ingredientId"
      >
        <span class="recipe-ingredient-name text-body-1 flex-grow-1" data-test="recipe-ingredient-name">{{ line.ingredient.name }}</span>
        <IngredientAmountField
          :entry-units="[line.ingredient.unit]"
          :saved-amount="line.amount"
          :label="t('admin.ingredients.labels.amount')"
          @amount-changed="changeAmount(line.ingredient.ingredientId, $event)"
        />
        <v-btn
          class="remove-recipe-line"
          data-test="remove-recipe-line"
          icon="mdi-delete"
          variant="text"
          color="error"
          @click="removeLine(line.ingredient.ingredientId)"
        />
      </div>
    </div>

    <div class="add-recipe-line d-flex align-start flex-wrap ga-2" data-test="add-recipe-line">
      <v-combobox
        v-model="chosenIngredient"
        class="added-ingredient flex-grow-1"
        maxlength="200"
        density="compact"
        hide-details
        :label="t('admin.ingredients.labels.ingredient')"
        :items="offeredIngredients"
        item-title="name"
        item-value="ingredientId"
      />
      <IngredientAmountField
        :key="addRowVersion"
        :entry-units="addedEntryUnits"
        :saved-amount="null"
        :label="t('admin.ingredients.labels.amount')"
        @amount-typed="noteAddedAmount"
      />
      <v-btn
        class="add-to-recipe"
        data-test="add-to-recipe"
        color="primary"
        :disabled="!addIsPossible || isAdding"
        @click="add"
      >
        {{ t('admin.ingredients.actions.addToRecipe') }}
      </v-btn>
    </div>
  </BaseFormDialog>
</template>
