<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminIngredientView, AdminItemView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { showsTheEmptyListText } from '../../../shared/core/emptyList'
import type { ParsedAmountInput } from '../../core/ingredientAmounts'
import { nameMatchesTheSearch } from '../../core/nameSearch'
import { useAdminIngredientsStore, type IngredientDraft } from '../../stores/ingredients'
import { useAdminItemsStore } from '../../stores/items'
import { useEditSession } from '../../composables/useEditSession'
import { useRowRefusals } from '../../composables/useRowRefusals'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import BaseEmptyListText from '../../../shared/components/BaseEmptyListText.vue'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'
import BaseIconAction from '../../../shared/components/BaseIconAction.vue'
import AdminLoadFailureBar from '../AdminLoadFailureBar.vue'
import BaseDeleteAction from '../BaseDeleteAction.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseListRow from '../BaseListRow.vue'
import BaseTrashButton from '../BaseTrashButton.vue'
import IngredientAmountField from '../ingredients/IngredientAmountField.vue'
import IngredientDialog from '../ingredients/IngredientDialog.vue'

interface IngredientRow {
  ingredient: AdminIngredientView
  deletionRefusal: string | null
  canBeAdded: boolean
}

const AMOUNT_INVALID_KEY = 'errors.admin.ingredients.amountInvalid'

const props = defineProps<{ item: AdminItemView }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const ingredients = useAdminIngredientsStore()
const items = useAdminItemsStore()
const lineRefusals = useRowRefusals()
const ingredientRefusals = useRowRefusals()
const ingredientIdOfLineToRemove = ref<string | null>(null)
const search = ref('')
const ingredientIdBeingAdded = ref<string | null>(null)
const amountToAdd = ref<ParsedAmountInput>({ kind: 'empty' })
const isAdding = ref(false)
const ingredientSession = useEditSession<AdminIngredientView, IngredientDraft, AdminIngredientView | null>({
  saveThrough: (draft, ingredient) =>
    ingredient === null
      ? ingredients.create(draft)
      : ingredients.save(ingredient.ingredientId, draft),
  afterSaving: (saved, mode) => {
    switch (mode) {
      case 'create':
        if (saved === null) {
          throw new Error('The laptop accepted a new ingredient without returning it.')
        }
        openTheAmountRowOfTheCreated(saved)
        return
      case 'edit':
        return
      default:
        assertNever(mode)
    }
  },
})
const {
  isOpen: isIngredientDialogOpen,
  edited: editedIngredient,
  refusalText: ingredientRefusalText,
} = ingredientSession

const recipeShowsTheEmptyListText = computed(() => showsTheEmptyListText(items, props.item.ingredients.length))

const ingredientIdsInTheRecipe = computed(
  () => new Set(props.item.ingredients.map((line) => line.ingredientId)),
)

const ingredientRows = computed<IngredientRow[]>(() =>
  ingredients.ingredients
    .filter((ingredient) => nameMatchesTheSearch(ingredient.name, search.value))
    .map((ingredient) => ({
      ingredient,
      deletionRefusal:
        ingredient.usedByItemNames.length > 0
          ? t('admin.ingredients.messages.usedByItems', { items: ingredient.usedByItemNames.join(', ') })
          : null,
      canBeAdded: !ingredientIdsInTheRecipe.value.has(ingredient.ingredientId),
    })),
)

const ingredientListShowsTheEmptyListText = computed(() =>
  showsTheEmptyListText(ingredients, ingredients.ingredients.length),
)

const nameForANewIngredient = computed(() => (ingredientRows.value.length === 0 ? search.value : ''))

const amountThatCanBeAdded = computed<number | null>(() => {
  const typed = amountToAdd.value
  switch (typed.kind) {
    case 'amount':
      return typed.amount > 0 ? typed.amount : null
    case 'empty':
    case 'unreadable':
      return null
    default:
      return assertNever(typed)
  }
})

function showsTheAmountRowOf(row: IngredientRow): boolean {
  return row.canBeAdded && row.ingredient.ingredientId === ingredientIdBeingAdded.value
}

async function changeAmount(ingredientId: string, parsed: ParsedAmountInput): Promise<void> {
  lineRefusals.forgetRefusalOf(ingredientId)
  switch (parsed.kind) {
    case 'amount':
      lineRefusals.showRefusalOf(
        ingredientId,
        await items.setIngredientAmount(props.item.itemId, ingredientId, parsed.amount),
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

function closeTheAmountRow(): void {
  const ingredientId = ingredientIdBeingAdded.value
  if (ingredientId !== null) {
    ingredientRefusals.forgetRefusalOf(ingredientId)
  }
  ingredientIdBeingAdded.value = null
  amountToAdd.value = { kind: 'empty' }
}

function openTheAmountRowOf(ingredientId: string): void {
  if (ingredientIdBeingAdded.value === ingredientId) {
    return
  }
  closeTheAmountRow()
  ingredientRefusals.forgetRefusalOf(ingredientId)
  ingredientIdBeingAdded.value = ingredientId
}

function openTheAmountRowOfTheCreated(ingredient: AdminIngredientView): void {
  if (!nameMatchesTheSearch(ingredient.name, search.value)) {
    search.value = ''
  }
  openTheAmountRowOf(ingredient.ingredientId)
}

function typeAmountToAdd(ingredientId: string, parsed: ParsedAmountInput): void {
  amountToAdd.value = parsed
  ingredientRefusals.forgetRefusalOf(ingredientId)
}

async function addToTheRecipe(ingredientId: string): Promise<void> {
  const amount = amountThatCanBeAdded.value
  if (amount === null || isAdding.value) {
    return
  }
  ingredientRefusals.forgetRefusalOf(ingredientId)
  isAdding.value = true
  const saved = await items.setIngredientAmount(props.item.itemId, ingredientId, amount)
  isAdding.value = false
  switch (saved.kind) {
    case 'ok':
      closeTheAmountRow()
      return
    case 'failed':
      ingredientRefusals.showRefusalOf(ingredientId, saved)
      return
    default:
      assertNever(saved)
  }
}

async function deleteIngredient(ingredientId: string): Promise<void> {
  ingredientRefusals.showRefusalOf(ingredientId, await ingredients.deleteIngredient(ingredientId))
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
    <AdminLoadFailureBar class="admin-section-gap" />
    <v-row>
      <v-col
        cols="6"
        class="recipe-column"
        data-test="recipe-column"
      >
        <h2 class="column-heading app-section-title admin-section-gap" data-test="column-heading">
          {{ t('admin.ingredients.labels.inTheRecipe') }}
        </h2>
        <BaseEmptyListText v-if="recipeShowsTheEmptyListText" />
        <BaseListRow
          v-for="(line, position) in item.ingredients"
          :key="line.ingredientId"
          class="recipe-line"
          data-test="recipe-line"
          :data-test-id="line.ingredientId"
          :position="position"
          :name="line.name"
          name-test-name="recipe-ingredient-name"
          :refusal-text="lineRefusals.refusalTextOf(line.ingredientId)"
        >
          <template #actions>
            <IngredientAmountField
              :unit-name="line.unitName"
              :saved-amount="line.amount"
              :label="t('admin.ingredients.labels.amount')"
              @amount-typed="lineRefusals.forgetRefusalOf(line.ingredientId)"
              @amount-changed="changeAmount(line.ingredientId, $event)"
            />
            <BaseTrashButton
              class="remove-recipe-line"
              data-test="remove-recipe-line"
              @click="ingredientIdOfLineToRemove = line.ingredientId"
            />
          </template>
        </BaseListRow>
      </v-col>

      <v-col
        cols="6"
        class="ingredients-column"
        data-test="ingredients-column"
      >
        <h2 class="column-heading app-section-title admin-section-gap" data-test="column-heading">
          {{ t('admin.ingredients.labels.allIngredients') }}
        </h2>
        <v-text-field
          v-model="search"
          class="ingredient-search admin-section-gap"
          data-test="ingredient-search"
          hide-details
          :label="t('admin.ingredients.labels.search')"
        />
        <BaseEmptyListText v-if="ingredientListShowsTheEmptyListText" />
        <BaseListRow
          v-for="(row, position) in ingredientRows"
          :key="row.ingredient.ingredientId"
          class="ingredient-row"
          data-test="ingredient-row"
          :data-test-id="row.ingredient.ingredientId"
          :position="position"
          :name="row.ingredient.name"
          name-test-name="ingredient-name"
          :refusal-text="ingredientRefusals.refusalTextOf(row.ingredient.ingredientId)"
        >
          <template #facts>
            <span class="ingredient-unit text-medium-emphasis" data-test="ingredient-unit">
              {{ t('admin.ingredients.labels.unitAfterName', { unit: row.ingredient.unitName }) }}
            </span>
          </template>
          <template #actions>
            <BaseEditButton
              class="edit-ingredient"
              data-test="edit-ingredient"
              @click="ingredientSession.openForEdit(row.ingredient)"
            />
            <BaseDeleteAction
              trash-test-name="delete-ingredient"
              :refusal-reason="row.deletionRefusal"
              :confirm-title="t('admin.ingredients.labels.deleteTitle')"
              :confirm-body="t('admin.ingredients.messages.deleteBody', { name: row.ingredient.name })"
              :confirm-label="t('admin.ingredients.actions.deleteConfirm')"
              @asked="ingredientRefusals.forgetRefusalOf(row.ingredient.ingredientId)"
              @confirmed="deleteIngredient(row.ingredient.ingredientId)"
            />
            <BaseIconAction
              v-if="row.canBeAdded"
              class="add-ingredient"
              data-test="add-ingredient"
              icon="mdi-plus"
              :label="t('admin.ingredients.actions.addToRecipe')"
              @click="openTheAmountRowOf(row.ingredient.ingredientId)"
            />
          </template>
          <div
            v-if="showsTheAmountRowOf(row)"
            class="amount-to-add admin-row-details d-flex align-start ga-2"
            data-test="amount-to-add"
          >
            <IngredientAmountField
              :unit-name="row.ingredient.unitName"
              :saved-amount="null"
              :label="t('admin.ingredients.labels.amount')"
              @amount-typed="typeAmountToAdd(row.ingredient.ingredientId, $event)"
              @keydown.enter="addToTheRecipe(row.ingredient.ingredientId)"
            />
            <v-btn
              class="add-to-recipe"
              data-test="add-to-recipe"
              color="primary"
              variant="tonal"
              :disabled="amountThatCanBeAdded === null || isAdding"
              @click="addToTheRecipe(row.ingredient.ingredientId)"
            >
              {{ t('admin.ingredients.actions.add') }}
            </v-btn>
          </div>
        </BaseListRow>
        <v-btn
          class="new-ingredient admin-block-gap"
          data-test="new-ingredient"
          color="primary"
          @click="ingredientSession.openForCreate"
        >
          {{ t('admin.ingredients.actions.new') }}
        </v-btn>
      </v-col>
    </v-row>
  </BaseFormDialog>

  <IngredientDialog
    v-if="isIngredientDialogOpen"
    :key="editedIngredient?.ingredientId ?? 'new'"
    :ingredient="editedIngredient"
    :error-text="ingredientRefusalText"
    :name-to-start-with="nameForANewIngredient"
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
