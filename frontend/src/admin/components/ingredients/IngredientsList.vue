<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminIngredientView } from '../../../shared/api/generatedSchemas'
import { useAdminIngredientsStore, type IngredientDraft } from '../../stores/ingredients'
import { useEditSession } from '../../composables/useEditSession'
import { useAmountUnitName } from '../../composables/useAmountUnitName'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import IngredientDialog from './IngredientDialog.vue'

interface IngredientRow {
  ingredient: AdminIngredientView
  unitWord: string
  isInAnActiveRecipe: boolean
  usedByActiveItemsText: string
}

const { t } = useI18n()
const ingredients = useAdminIngredientsStore()
const unitName = useAmountUnitName()
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const ingredientSession = useEditSession<AdminIngredientView, IngredientDraft, AdminIngredientView | null>({
  saveThrough: (draft, ingredient) =>
    ingredient === null
      ? ingredients.create(draft)
      : ingredients.save(ingredient.ingredientId, draft),
})
const {
  isOpen: isIngredientDialogOpen,
  edited: editedIngredient,
  refusalText,
} = ingredientSession
let stopListening: (() => void) | null = null

const rows = computed<IngredientRow[]>(() =>
  ingredients.ingredients
    .filter((ingredient) => showsDeactivated.value || ingredient.isActive)
    .map((ingredient) => ({
      ingredient,
      unitWord: unitName(ingredient.unit),
      isInAnActiveRecipe: ingredient.activeItemNames.length > 0,
      usedByActiveItemsText: t('admin.ingredients.messages.usedByActiveItems', {
        items: ingredient.activeItemNames.join(', '),
      }),
    })),
)

const showsNoneYet = computed(
  () => ingredients.ingredients.length === 0 && !ingredients.loadFailed,
)

const showsRefusal = computed(
  () => refusalText.value !== null && !isIngredientDialogOpen.value,
)

async function deactivate(): Promise<void> {
  const ingredientId = askingAboutId.value
  askingAboutId.value = null
  if (ingredientId !== null) {
    ingredientSession.forgetRefusal()
    ingredientSession.showRefusalOf(await ingredients.setActive(ingredientId, false))
  }
}

async function reactivate(ingredientId: string): Promise<void> {
  ingredientSession.forgetRefusal()
  ingredientSession.showRefusalOf(await ingredients.setActive(ingredientId, true))
}

onMounted(async () => {
  stopListening = ingredients.listen()
  await ingredients.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-ingredients" data-test="admin-ingredients">
    <div class="admin-heading d-flex align-center flex-wrap justify-space-between ga-2 mb-4">
      <h1 class="text-h5">{{ t('admin.ingredients.title') }}</h1>
      <v-checkbox
        v-model="showsDeactivated"
        class="show-deactivated"
        data-test="show-deactivated"
        density="compact"
        hide-details
        :label="t('admin.common.actions.showDeactivated')"
      />
    </div>

    <v-alert
      v-if="showsRefusal"
      class="refusal mb-4"
      data-test="refusal"
      type="warning"
      variant="tonal"
    >
      {{ refusalText }}
    </v-alert>
    <v-alert
      v-if="ingredients.loadFailed"
      class="error"
      data-test="load-failed"
      type="error"
      variant="tonal"
    >
      {{ t('admin.common.errors.loadFailed') }}
    </v-alert>

    <p v-if="showsNoneYet" class="none-yet text-medium-emphasis mb-4" data-test="none-yet">
      {{ t('admin.ingredients.messages.noneYet') }}
    </p>

    <v-card
      v-for="row in rows"
      :key="row.ingredient.ingredientId"
      class="ingredient-row mb-2"
      data-test="ingredient-row"
      :data-test-id="row.ingredient.ingredientId"
    >
      <div class="d-flex align-center flex-wrap ga-2 px-4 py-2">
        <span class="name text-h6" data-test="name">{{ row.ingredient.name }}</span>
        <span class="unit text-body-1 text-medium-emphasis" data-test="unit">{{ row.unitWord }}</span>
        <v-chip
          v-if="!row.ingredient.isActive"
          class="deactivated"
          data-test="deactivated"
          size="small"
          color="grey"
        >
          {{ t('admin.common.labels.deactivated') }}
        </v-chip>
        <v-spacer />
        <v-btn
          class="edit"
          data-test="edit"
          variant="text"
          @click="ingredientSession.openForEdit(row.ingredient)"
        >
          {{ t('admin.common.actions.edit') }}
        </v-btn>
        <span v-if="row.ingredient.isActive" class="deactivate-wrapper" data-test="deactivate-wrapper">
          <v-btn
            class="deactivate"
            data-test="deactivate"
            icon="mdi-delete"
            variant="text"
            color="error"
            :disabled="row.isInAnActiveRecipe"
            @click="askingAboutId = row.ingredient.ingredientId"
          />
          <v-tooltip
            data-test="used-by-active-items-tooltip"
            :data-test-id="row.ingredient.ingredientId"
            activator="parent"
            location="top"
            :disabled="!row.isInAnActiveRecipe"
          >
            {{ row.usedByActiveItemsText }}
          </v-tooltip>
        </span>
        <v-btn
          v-else
          class="reactivate"
          data-test="reactivate"
          variant="text"
          @click="reactivate(row.ingredient.ingredientId)"
        >
          {{ t('admin.ingredients.actions.activate') }}
        </v-btn>
      </div>
    </v-card>

    <v-btn
      class="new-ingredient mt-6"
      data-test="new-ingredient"
      color="primary"
      @click="ingredientSession.openForCreate"
    >
      {{ t('admin.ingredients.actions.new') }}
    </v-btn>

    <IngredientDialog
      v-if="isIngredientDialogOpen"
      :key="editedIngredient?.ingredientId ?? 'new'"
      :ingredient="editedIngredient"
      :error-text="refusalText"
      @save="ingredientSession.save"
      @cancel="ingredientSession.close"
    />
    <BaseConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.ingredients.labels.deactivateTitle')"
      :confirm-label="t('admin.ingredients.actions.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
  </v-container>
</template>

<style scoped>
.deactivate-wrapper {
  display: inline-flex;
}
</style>
