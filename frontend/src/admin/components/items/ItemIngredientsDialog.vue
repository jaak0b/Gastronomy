<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AdminIngredientView,
  AdminItemIngredientView,
  AdminItemView,
} from '../../../shared/api/generatedSchemas'
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
const ingredientIdsBeingAssigned = ref<Set<string>>(new Set())
const ingredientIdToFocus = ref<string | null>(null)
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
        revealTheCreatedIngredient(saved)
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

const ingredientsById = computed(
  () => new Map(ingredients.ingredients.map((ingredient) => [ingredient.ingredientId, ingredient])),
)

const ingredientRows = computed<IngredientRow[]>(() =>
  ingredients.ingredients
    .filter(
      (ingredient) =>
        !ingredientIdsInTheRecipe.value.has(ingredient.ingredientId) &&
        nameMatchesTheSearch(ingredient.name, search.value),
    )
    .map((ingredient) => ({
      ingredient,
      deletionRefusal:
        ingredient.usedByItemNames.length > 0
          ? t('admin.ingredients.messages.usedByItems', { items: ingredient.usedByItemNames.join(', ') })
          : null,
    })),
)

const ingredientListShowsTheEmptyListText = computed(() =>
  showsTheEmptyListText(ingredients, ingredients.ingredients.length),
)

const isAssigningAnIngredient = computed(() => ingredientIdsBeingAssigned.value.size > 0)

const nameForANewIngredient = computed(() => (ingredientRows.value.length === 0 ? search.value : ''))

async function changeAmount(ingredientId: string, parsed: ParsedAmountInput): Promise<void> {
  lineRefusals.forgetRefusalOf(ingredientId)
  switch (parsed.kind) {
    case 'amount':
      if (parsed.amount <= 0) {
        lineRefusals.refuse(ingredientId, AMOUNT_INVALID_KEY)
        return
      }
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

function openTheFormOfTheLine(line: AdminItemIngredientView): void {
  const ingredient = ingredientsById.value.get(line.ingredientId)
  if (ingredient !== undefined) {
    ingredientSession.openForEdit(ingredient)
  }
}

function revealTheCreatedIngredient(ingredient: AdminIngredientView): void {
  if (!nameMatchesTheSearch(ingredient.name, search.value)) {
    search.value = ''
  }
}

function isBeingAssigned(ingredientId: string): boolean {
  return ingredientIdsBeingAssigned.value.has(ingredientId)
}

async function addToTheRecipe(ingredientId: string): Promise<void> {
  if (isBeingAssigned(ingredientId)) {
    return
  }
  ingredientRefusals.forgetRefusalOf(ingredientId)
  ingredientIdsBeingAssigned.value.add(ingredientId)
  const saved = await items.setIngredientAmount(props.item.itemId, ingredientId, 1)
  ingredientIdsBeingAssigned.value.delete(ingredientId)
  switch (saved.kind) {
    case 'ok':
      ingredientIdToFocus.value = ingredientId
      return
    case 'failed':
      ingredientRefusals.showRefusalOf(ingredientId, saved)
      return
    default:
      assertNever(saved)
  }
}

function amountFieldWasFocused(ingredientId: string): void {
  if (ingredientIdToFocus.value === ingredientId) {
    ingredientIdToFocus.value = null
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
    :busy="isAssigningAnIngredient"
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
            <div class="recipe-line-actions">
              <IngredientAmountField
                :unit-name="line.unitName"
                :saved-amount="line.amount"
                :label="t('admin.ingredients.labels.amount')"
                :amount-when-cleared="1"
                :focus-on-mount="line.ingredientId === ingredientIdToFocus"
                @amount-typed="lineRefusals.forgetRefusalOf(line.ingredientId)"
                @amount-changed="changeAmount(line.ingredientId, $event)"
                @focused="amountFieldWasFocused(line.ingredientId)"
              />
              <BaseEditButton
                v-if="ingredientsById.has(line.ingredientId)"
                class="edit-recipe-ingredient"
                data-test="edit-recipe-ingredient"
                @click="openTheFormOfTheLine(line)"
              />
              <BaseTrashButton
                class="remove-recipe-line"
                data-test="remove-recipe-line"
                @click="ingredientIdOfLineToRemove = line.ingredientId"
              />
            </div>
          </template>
        </BaseListRow>
      </v-col>

      <v-col
        cols="6"
        class="ingredients-column"
        data-test="ingredients-column"
      >
        <h2 class="column-heading app-section-title admin-section-gap" data-test="column-heading">
          {{ t('admin.ingredients.labels.availableIngredients') }}
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
              class="add-ingredient"
              data-test="add-ingredient"
              icon="mdi-plus"
              :label="t('admin.ingredients.actions.addToRecipe')"
              :disabled="isBeingAssigned(row.ingredient.ingredientId)"
              @click="addToTheRecipe(row.ingredient.ingredientId)"
            />
          </template>
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

<style scoped>
.recipe-line :deep(.row-name),
.ingredient-row :deep(.row-name) {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.recipe-line :deep(.row-line) {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 0 auto;
}

.recipe-line-actions {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  gap: var(--admin-row-gap);
  min-width: 0;
}

.recipe-line-actions :deep(.ingredient-amount-field) {
  flex: 1 1 auto;
  min-width: 0;
}

.recipe-line-actions :deep(.amount-input) {
  flex: 0 0 6rem;
  min-width: 6rem;
}

.recipe-line-actions :deep(.edit-recipe-ingredient),
.recipe-line-actions :deep(.remove-recipe-line) {
  flex: 0 0 auto;
}
</style>
