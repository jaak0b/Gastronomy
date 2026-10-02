<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminActionResult } from '../../core/adminActionResult'
import {
  adminErrorMessageForKey,
  type AdminErrorMessage,
} from '../../core/adminErrorMessage'
import type { ParsedAmountInput } from '../../core/ingredientAmounts'
import type {
  AdminIngredientView,
  AdminItemView,
  IngredientUnit,
} from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminIngredientsStore, type IngredientDraft } from '../../stores/ingredients'
import { useAdminItemsStore } from '../../stores/items'
import { useIngredientUnitChoices } from '../../composables/useIngredientUnitChoices'
import { useRefusalText } from '../../composables/useRefusalText'
import BaseConfirmDialog from '../BaseConfirmDialog.vue'
import BaseFormDialog from '../BaseFormDialog.vue'
import IngredientAmountField from '../ingredients/IngredientAmountField.vue'
import IngredientEditLine from '../ingredients/IngredientEditLine.vue'

type AddMode = 'existing' | 'new'

interface RecipeLine {
  ingredient: AdminIngredientView
  amount: number
}

const AMOUNT_INVALID_KEY = 'errors.admin.ingredients.amountInvalid'

const props = defineProps<{ item: AdminItemView }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const ingredients = useAdminIngredientsStore()
const items = useAdminItemsStore()
const unitChoices = useIngredientUnitChoices()
const refusal = ref<AdminErrorMessage | null>(null)
const addMode = ref<AddMode>('existing')
const pickedIngredientId = ref<string | null>(null)
const newName = ref('')
const newUnit = ref<IngredientUnit>('gram')
const addedAmount = ref<ParsedAmountInput>({ kind: 'empty' })
const addRowVersion = ref(0)
const isAdding = ref(false)
const deactivatedIngredientId = ref<string | null>(null)

const refusalText = useRefusalText(refusal)

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

const addedUnit = computed<IngredientUnit | null>(() => {
  switch (addMode.value) {
    case 'new':
      return newUnit.value
    case 'existing':
      return pickedIngredientId.value === null
        ? null
        : (ingredients.findIngredientWithId(pickedIngredientId.value)?.unit ?? null)
    default:
      return assertNever(addMode.value)
  }
})

const addIsImpossible = computed(() => {
  switch (addMode.value) {
    case 'new':
      return newName.value.trim().length === 0
    case 'existing':
      return pickedIngredientId.value === null
    default:
      return assertNever(addMode.value)
  }
})

function wasRefused(result: AdminActionResult<unknown>): boolean {
  switch (result.kind) {
    case 'ok':
      return false
    case 'failed':
      refusal.value = result.message
      return true
    default:
      return assertNever(result)
  }
}

function amountFrom(parsed: ParsedAmountInput): number | null {
  switch (parsed.kind) {
    case 'amount':
      return parsed.baseAmount
    case 'empty':
    case 'unreadable':
      refusal.value = adminErrorMessageForKey(AMOUNT_INVALID_KEY)
      return null
    default:
      return assertNever(parsed)
  }
}

async function changeAmount(ingredientId: string, parsed: ParsedAmountInput): Promise<void> {
  refusal.value = null
  const amount = amountFrom(parsed)
  if (amount !== null) {
    wasRefused(await items.setIngredientAmount(props.item.itemId, ingredientId, amount))
  }
}

async function removeLine(ingredientId: string): Promise<void> {
  refusal.value = null
  wasRefused(await items.removeIngredient(props.item.itemId, ingredientId))
}

async function ingredientToAdd(): Promise<string | null> {
  switch (addMode.value) {
    case 'existing':
      return pickedIngredientId.value
    case 'new': {
      const created = await ingredients.create({ name: newName.value, unit: newUnit.value })
      switch (created.kind) {
        case 'ok':
          pickedIngredientId.value = created.value.ingredientId
          addMode.value = 'existing'
          newName.value = ''
          return created.value.ingredientId
        case 'failed':
          refusal.value = created.message
          return null
        default:
          return assertNever(created)
      }
    }
    default:
      return assertNever(addMode.value)
  }
}

async function add(): Promise<void> {
  refusal.value = null
  const amount = amountFrom(addedAmount.value)
  if (amount === null) {
    return
  }
  isAdding.value = true
  const ingredientId = await ingredientToAdd()
  if (ingredientId === null) {
    isAdding.value = false
    return
  }
  const saved = await items.setIngredientAmount(props.item.itemId, ingredientId, amount)
  isAdding.value = false
  if (!wasRefused(saved)) {
    pickedIngredientId.value = null
    addedAmount.value = { kind: 'empty' }
    addRowVersion.value += 1
  }
}

function switchAddMode(mode: AddMode): void {
  refusal.value = null
  addMode.value = mode
}

async function saveIngredient(ingredientId: string, draft: IngredientDraft): Promise<void> {
  refusal.value = null
  wasRefused(await ingredients.save(ingredientId, draft))
}

async function activateIngredient(ingredientId: string): Promise<void> {
  refusal.value = null
  wasRefused(await ingredients.setActive(ingredientId, true))
}

async function deactivateIngredient(): Promise<void> {
  const ingredientId = deactivatedIngredientId.value
  deactivatedIngredientId.value = null
  if (ingredientId !== null) {
    refusal.value = null
    wasRefused(await ingredients.setActive(ingredientId, false))
  }
}
</script>

<template>
  <BaseFormDialog
    :title="t('admin.ingredients.labels.recipeTitle', { item: item.name })"
    :error-text="refusalText"
    :save-label="t('admin.ingredients.actions.add')"
    :cancel-label="t('admin.ingredients.actions.close')"
    :save-disabled="addIsImpossible"
    :busy="isAdding"
    @save="add"
    @cancel="emit('close')"
  >
    <div class="recipe-lines mb-4">
      <div
        v-for="line in recipeLines"
        :key="line.ingredient.ingredientId"
        class="recipe-line d-flex align-center flex-wrap ga-2 py-1"
      >
        <span class="recipe-ingredient-name text-body-1 flex-grow-1">{{ line.ingredient.name }}</span>
        <IngredientAmountField
          :unit="line.ingredient.unit"
          :saved-amount="line.amount"
          :label="t('admin.ingredients.labels.amount')"
          @commit="changeAmount(line.ingredient.ingredientId, $event)"
        />
        <v-btn
          class="remove-recipe-line"
          icon="mdi-delete"
          variant="text"
          color="error"
          @click="removeLine(line.ingredient.ingredientId)"
        />
      </div>
    </div>

    <div class="add-recipe-line d-flex align-start flex-wrap ga-2">
      <v-select
        v-if="addMode === 'existing'"
        v-model="pickedIngredientId"
        class="picked-ingredient flex-grow-1"
        density="compact"
        hide-details
        :label="t('admin.ingredients.labels.ingredient')"
        :items="offeredIngredients"
        item-title="name"
        item-value="ingredientId"
        :no-data-text="t('admin.ingredients.messages.noneYet')"
      />
      <template v-else>
        <v-text-field
          v-model="newName"
          class="new-ingredient-name flex-grow-1"
          maxlength="200"
          density="compact"
          hide-details
          :label="t('admin.ingredients.labels.name')"
        />
        <v-select
          v-model="newUnit"
          class="new-ingredient-unit"
          density="compact"
          hide-details
          :label="t('admin.ingredients.labels.unit')"
          :items="unitChoices"
        />
      </template>
      <IngredientAmountField
        v-if="addedUnit !== null"
        :key="`${addedUnit}-${addRowVersion}`"
        :unit="addedUnit"
        :saved-amount="null"
        :label="t('admin.ingredients.labels.amount')"
        @change="addedAmount = $event"
      />
    </div>
    <v-btn
      v-if="addMode === 'existing'"
      class="start-new-ingredient mt-2"
      variant="text"
      @click="switchAddMode('new')"
    >
      {{ t('admin.ingredients.actions.new') }}
    </v-btn>
    <v-btn
      v-else
      class="choose-existing-ingredient mt-2"
      variant="text"
      @click="switchAddMode('existing')"
    >
      {{ t('admin.ingredients.actions.chooseExisting') }}
    </v-btn>

    <v-expansion-panels class="all-ingredients mt-4" variant="accordion">
      <v-expansion-panel>
        <v-expansion-panel-title class="all-ingredients-title">
          {{ t('admin.ingredients.labels.allIngredients') }}
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <IngredientEditLine
            v-for="ingredient in ingredients.ingredients"
            :key="ingredient.ingredientId"
            :ingredient="ingredient"
            @save="saveIngredient(ingredient.ingredientId, $event)"
            @activate="activateIngredient(ingredient.ingredientId)"
            @deactivate="deactivatedIngredientId = ingredient.ingredientId"
          />
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>
  </BaseFormDialog>
  <BaseConfirmDialog
    v-if="deactivatedIngredientId !== null"
    :title="t('admin.ingredients.labels.deactivateTitle')"
    :body="t('admin.ingredients.messages.deactivateCost')"
    :confirm-label="t('admin.ingredients.actions.deactivateConfirm')"
    @confirm="deactivateIngredient"
    @cancel="deactivatedIngredientId = null"
  />
</template>
