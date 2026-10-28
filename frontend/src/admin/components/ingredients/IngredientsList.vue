<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminIngredientView } from '../../../shared/api/generatedSchemas'
import { combineReleases } from '../../../shared/core/combineReleases'
import { useAdminIngredientsStore, type IngredientDraft } from '../../stores/ingredients'
import { useAdminUnitsStore } from '../../stores/units'
import { useEditSession } from '../../composables/useEditSession'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseHeadingWithCheckbox from '../BaseHeadingWithCheckbox.vue'
import BaseShowButton from '../BaseShowButton.vue'
import BaseTrashButton from '../BaseTrashButton.vue'
import IngredientDialog from './IngredientDialog.vue'

interface IngredientRow {
  ingredient: AdminIngredientView
  deactivationRefusal: string | null
}

const { t } = useI18n()
const ingredients = useAdminIngredientsStore()
const units = useAdminUnitsStore()
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
      deactivationRefusal:
        ingredient.activeItemNames.length > 0
          ? t('admin.ingredients.messages.usedByActiveItems', { items: ingredient.activeItemNames.join(', ') })
          : null,
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
  stopListening = combineReleases(ingredients.listen(), units.listen())
  await Promise.all([ingredients.load(), units.load()])
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-ingredients" data-test="admin-ingredients">
    <BaseHeadingWithCheckbox
      v-model="showsDeactivated"
      class="admin-heading mb-4"
      :checkbox-label="t('admin.common.actions.showDeactivated')"
      checkbox-test-name="show-deactivated"
    >
      <h1 class="text-h5">{{ t('admin.ingredients.title') }}</h1>
    </BaseHeadingWithCheckbox>

    <v-alert
      v-if="showsRefusal"
      class="refusal mb-4"
      data-test="refusal"
      type="warning"
      variant="tonal"
    >
      {{ refusalText }}
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
        <span class="unit text-body-1 text-medium-emphasis" data-test="unit">{{ row.ingredient.unitName }}</span>
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
        <BaseEditButton
          class="edit"
          data-test="edit"
          @click="ingredientSession.openForEdit(row.ingredient)"
        />
        <BaseActionWithRefusalReason
          v-if="row.ingredient.isActive"
          class="deactivate-wrapper"
          data-test="deactivate-wrapper"
          :refusal-reason="row.deactivationRefusal"
        >
          <template #default="{ refused }">
            <BaseTrashButton
              class="deactivate"
              data-test="deactivate"
              :disabled="refused"
              @click="askingAboutId = row.ingredient.ingredientId"
            />
          </template>
        </BaseActionWithRefusalReason>
        <BaseShowButton
          v-else
          class="reactivate"
          data-test="reactivate"
          :label="t('admin.ingredients.actions.activate')"
          @click="reactivate(row.ingredient.ingredientId)"
        />
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
