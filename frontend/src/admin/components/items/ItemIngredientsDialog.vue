<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { entryUnitsFor, type ParsedAmountInput } from '../../core/ingredientAmounts'
import type { AdminIngredientView, AdminItemView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminIngredientsStore, type IngredientDraft } from '../../stores/ingredients'
import { useAdminItemsStore } from '../../stores/items'
import { useEditSession } from '../../composables/useEditSession'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import { useRowRefusals } from '../../composables/useRowRefusals'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'
import IngredientAmountField from '../ingredients/IngredientAmountField.vue'
import IngredientDialog from '../ingredients/IngredientDialog.vue'

interface RecipeLine {
  ingredient: AdminIngredientView
  amount: number
  refusalText: string | null
}

const AMOUNT_INVALID_KEY = 'errors.admin.ingredients.amountInvalid'

const props = defineProps<{ item: AdminItemView }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const ingredients = useAdminIngredientsStore()
const items = useAdminItemsStore()
const lineRefusals = useRowRefusals()
const ingredientIdOfLineToRemove = ref<string | null>(null)
const chosenIngredientId = ref<string | null>(null)
const addedAmount = ref<ParsedAmountInput>({ kind: 'empty' })
const isAdding = ref(false)
const {
  refusal: addRefusal,
  refusalText: addRefusalText,
  showRefusalOf: showAddRefusalOf,
} = useRefusalDisplay()
const ingredientSession = useEditSession<AdminIngredientView, IngredientDraft, AdminIngredientView>({
  saveThrough: (draft) => ingredients.create(draft),
  afterSaving: (created) => {
    chooseIngredientToAdd(created.ingredientId)
  },
})
const { isOpen: isIngredientDialogOpen, refusalText: ingredientRefusalText } = ingredientSession

const recipeLines = computed<RecipeLine[]>(() =>
  props.item.ingredients.flatMap((line) => {
    const ingredient = ingredients.findIngredientWithId(line.ingredientId)
    return ingredient === null
      ? []
      : [{ ingredient, amount: line.amount, refusalText: lineRefusals.refusalTextOf(line.ingredientId) }]
  }),
)

const offeredIngredients = computed(() => {
  const inTheRecipe = new Set(props.item.ingredients.map((line) => line.ingredientId))
  return ingredients.ingredients.filter(
    (ingredient) => ingredient.isActive && !inTheRecipe.has(ingredient.ingredientId),
  )
})

const chosenIngredient = computed(
  () =>
    offeredIngredients.value.find(
      (ingredient) => ingredient.ingredientId === chosenIngredientId.value,
    ) ?? null,
)

const addIsPossible = computed(
  () =>
    chosenIngredient.value !== null &&
    addedAmount.value.kind === 'amount' &&
    addedAmount.value.baseAmount > 0,
)

async function changeAmount(ingredientId: string, parsed: ParsedAmountInput): Promise<void> {
  lineRefusals.forgetRefusalOf(ingredientId)
  switch (parsed.kind) {
    case 'amount':
      lineRefusals.showRefusalOf(
        ingredientId,
        await items.setIngredientAmount(props.item.itemId, ingredientId, parsed.baseAmount),
      )
      return
    case 'empty':
    case 'unreadable':
      lineRefusals.refuse(ingredientId, AMOUNT_INVALID_KEY)
      return
    default:
      assertNever(parsed)
  }
}

async function removeLineFromRecipe(): Promise<void> {
  const ingredientId = ingredientIdOfLineToRemove.value
  ingredientIdOfLineToRemove.value = null
  if (ingredientId === null) {
    return
  }
  lineRefusals.forgetRefusalOf(ingredientId)
  lineRefusals.showRefusalOf(ingredientId, await items.removeIngredient(props.item.itemId, ingredientId))
}

function chooseIngredientToAdd(ingredientId: string | null): void {
  chosenIngredientId.value = ingredientId
  addRefusal.value = null
}

function typeAmountToAdd(parsed: ParsedAmountInput): void {
  addedAmount.value = parsed
  addRefusal.value = null
}

async function add(): Promise<void> {
  const ingredient = chosenIngredient.value
  const amount = addedAmount.value
  if (ingredient === null || amount.kind !== 'amount') {
    return
  }
  addRefusal.value = null
  isAdding.value = true
  const saved = await items.setIngredientAmount(props.item.itemId, ingredient.ingredientId, amount.baseAmount)
  isAdding.value = false
  if (!showAddRefusalOf(saved)) {
    chosenIngredientId.value = null
    addedAmount.value = { kind: 'empty' }
  }
}
</script>

<template>
  <BaseFormDialog
    :title="t('admin.ingredients.labels.recipeTitle', { item: item.name })"
    :error-text="null"
    :cancel-label="t('admin.ingredients.actions.close')"
    :busy="isAdding"
    close-only
    wide
    @cancel="emit('close')"
  >
    <div class="recipe-lines mb-4">
      <div
        v-for="line in recipeLines"
        :key="line.ingredient.ingredientId"
        class="recipe-line"
        data-test="recipe-line"
        :data-test-id="line.ingredient.ingredientId"
      >
        <div class="d-flex align-center flex-wrap ga-2 py-1">
          <span class="recipe-ingredient-name text-body-1 flex-grow-1" data-test="recipe-ingredient-name">{{ line.ingredient.name }}</span>
          <IngredientAmountField
            :entry-units="[line.ingredient.unit]"
            :saved-amount="line.amount"
            :label="t('admin.ingredients.labels.amount')"
            @amount-typed="lineRefusals.forgetRefusalOf(line.ingredient.ingredientId)"
            @amount-changed="changeAmount(line.ingredient.ingredientId, $event)"
          />
          <v-btn
            class="remove-recipe-line"
            data-test="remove-recipe-line"
            icon="mdi-delete"
            variant="text"
            color="error"
            @click="ingredientIdOfLineToRemove = line.ingredient.ingredientId"
          />
        </div>
        <v-alert
          v-if="line.refusalText !== null"
          class="refusal mb-2"
          data-test="refusal"
          type="warning"
          variant="tonal"
        >
          {{ line.refusalText }}
        </v-alert>
      </div>
    </div>

    <div class="add-recipe-line" data-test="add-recipe-line">
      <div class="d-flex align-start flex-wrap ga-2">
        <v-autocomplete
          :model-value="chosenIngredientId"
          class="ingredient-search"
          data-test="ingredient-search"
          density="compact"
          hide-details
          :label="t('admin.ingredients.labels.ingredient')"
          :items="offeredIngredients"
          item-title="name"
          item-value="ingredientId"
          :no-data-text="t('admin.ingredients.messages.noIngredientsToAdd')"
          @update:model-value="chooseIngredientToAdd"
        />
        <IngredientAmountField
          v-if="chosenIngredient !== null"
          :entry-units="entryUnitsFor(chosenIngredient.unit)"
          :saved-amount="null"
          :label="t('admin.ingredients.labels.amount')"
          @amount-typed="typeAmountToAdd"
        />
        <v-text-field
          v-else
          class="amount-before-choosing"
          data-test="amount-input"
          density="compact"
          hide-details
          disabled
          :label="t('admin.ingredients.labels.amount')"
        />
        <v-btn
          class="add-to-recipe"
          data-test="add-to-recipe"
          color="primary"
          variant="tonal"
          :disabled="!addIsPossible || isAdding"
          @click="add"
        >
          {{ t('admin.ingredients.actions.addToRecipe') }}
        </v-btn>
        <v-btn
          class="new-ingredient"
          data-test="new-ingredient"
          variant="text"
          @click="ingredientSession.openForCreate()"
        >
          {{ t('admin.ingredients.actions.new') }}
        </v-btn>
      </div>
      <v-alert
        v-if="addRefusalText !== null"
        class="refusal mt-2"
        data-test="refusal"
        type="warning"
        variant="tonal"
      >
        {{ addRefusalText }}
      </v-alert>
    </div>
  </BaseFormDialog>

  <IngredientDialog
    v-if="isIngredientDialogOpen"
    :ingredient="null"
    :error-text="ingredientRefusalText"
    @save="ingredientSession.save"
    @cancel="ingredientSession.close"
  />

  <BaseConfirmDialog
    v-if="ingredientIdOfLineToRemove !== null"
    :title="t('admin.ingredients.labels.removeFromRecipeTitle')"
    :body="t('admin.ingredients.messages.removeFromRecipeBody', { item: item.name })"
    :confirm-label="t('admin.ingredients.actions.removeFromRecipe')"
    @confirm="removeLineFromRecipe"
    @cancel="ingredientIdOfLineToRemove = null"
  />
</template>

<style scoped>
.ingredient-search {
  flex: 1 1 14rem;
}

.amount-before-choosing {
  flex: 0 0 9rem;
}
</style>
