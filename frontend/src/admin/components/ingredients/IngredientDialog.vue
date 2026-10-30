<script setup lang="ts">
import { computed, ref, shallowRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminIngredientView, AdminUnitView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import type { IngredientDraft } from '../../stores/ingredients'
import { useAdminUnitsStore, type UnitDraft } from '../../stores/units'
import { useEditSession } from '../../composables/useEditSession'
import { ADMIN_CHIP_BUTTON_DEFAULTS } from '../../styles/adminSizes'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'
import BaseRefusalAlert from '../../../shared/components/BaseRefusalAlert.vue'
import AdminLoadFailureBar from '../AdminLoadFailureBar.vue'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseTrashButton from '../BaseTrashButton.vue'
import UnitDialog from '../units/UnitDialog.vue'

interface UnitChoice {
  unitId: string
  name: string
  isListed: boolean
  deletionRefusal: string | null
}

const props = defineProps<{ ingredient: AdminIngredientView | null; errorText: string | null }>()
const emit = defineEmits<{ save: [draft: IngredientDraft]; cancel: [] }>()

const { t } = useI18n()
const units = useAdminUnitsStore()
const name = ref(props.ingredient?.name ?? '')
const unitId = ref<string | null>(props.ingredient?.unitId ?? null)
const unitAwaitingDeleteConfirmation = shallowRef<UnitChoice | null>(null)
const unitSession = useEditSession<UnitChoice, UnitDraft, AdminUnitView | null>({
  saveThrough: (draft, unit) =>
    unit === null
      ? units.create(draft)
      : units.save(unit.unitId, draft),
  afterSaving: (saved, mode) => {
    switch (mode) {
      case 'create':
        if (saved === null) {
          throw new Error('The laptop accepted a new unit without returning it.')
        }
        unitId.value = saved.unitId
        return
      case 'edit':
        return
      default:
        assertNever(mode)
    }
  },
})
const {
  isOpen: isUnitDialogOpen,
  edited: editedUnit,
  refusalText: unitRefusalText,
} = unitSession

const unitChoices = computed<UnitChoice[]>(() => {
  const listed = units.units.map((unit) => ({
    unitId: unit.unitId,
    name: unit.name,
    isListed: true,
    deletionRefusal:
      unit.usedByIngredientNames.length > 0
        ? t('admin.units.messages.usedByIngredients', { ingredients: unit.usedByIngredientNames.join(', ') })
        : null,
  }))
  const current = props.ingredient
  if (current === null || listed.some((choice) => choice.unitId === current.unitId)) {
    return listed
  }
  return [
    ...listed,
    { unitId: current.unitId, name: current.unitName, isListed: false, deletionRefusal: null },
  ]
})

const title = computed(() =>
  props.ingredient === null
    ? t('admin.ingredients.actions.new')
    : t('admin.ingredients.labels.editTitle'),
)

const isIncomplete = computed(() => name.value.trim().length === 0 || unitId.value === null)

function askToDelete(unit: UnitChoice): void {
  unitSession.forgetRefusal()
  unitAwaitingDeleteConfirmation.value = unit
}

async function deleteTheUnitAwaitingConfirmation(): Promise<void> {
  const unit = unitAwaitingDeleteConfirmation.value
  unitAwaitingDeleteConfirmation.value = null
  if (unit === null || unitSession.showRefusalOf(await units.deleteUnit(unit.unitId))) {
    return
  }
  if (unitId.value === unit.unitId) {
    unitId.value = null
  }
}

function save(): void {
  const chosenUnitId = unitId.value
  if (isIncomplete.value || chosenUnitId === null) {
    return
  }
  emit('save', { name: name.value, unitId: chosenUnitId })
}
</script>

<template>
  <BaseFormDialog
    :title="title"
    :error-text="errorText"
    :save-disabled="isIncomplete"
    @save="save"
    @cancel="emit('cancel')"
  >
    <AdminLoadFailureBar class="admin-section-gap" />
    <v-text-field
      v-model="name"
      class="ingredient-name-field admin-section-gap"
      data-test="ingredient-name-field"
      maxlength="200"
      :label="t('admin.ingredients.labels.name')"
    />
    <div class="ingredient-unit-field" data-test="ingredient-unit-field">
      <div class="text-medium-emphasis" data-test="ingredient-unit-label">
        {{ t('admin.ingredients.labels.unit') }}
      </div>
      <v-defaults-provider :defaults="ADMIN_CHIP_BUTTON_DEFAULTS">
        <div class="d-flex flex-wrap align-center">
          <v-chip-group
            v-model="unitId"
            column
            filter
            mandatory
            color="primary"
            @update:model-value="unitSession.forgetRefusal()"
          >
            <v-chip
              v-for="choice in unitChoices"
              :key="choice.unitId"
              class="unit-chip"
              data-test="unit-chip"
              :data-test-id="choice.unitId"
              :value="choice.unitId"
            >
              <span data-test="unit-name">{{ choice.name }}</span>
              <template v-if="choice.unitId === unitId || choice.isListed" #append>
                <BaseEditButton
                  v-if="choice.unitId === unitId"
                  class="rename-unit"
                  data-test="rename-unit"
                  @click.stop="unitSession.openForEdit(choice)"
                  @keydown.enter.stop
                  @keydown.space.stop
                />
                <BaseActionWithRefusalReason
                  v-if="choice.isListed"
                  class="delete-unit-wrapper"
                  data-test="delete-unit-wrapper"
                  :refusal-reason="choice.deletionRefusal"
                  @click.stop
                  @keydown.enter.stop
                  @keydown.space.stop
                >
                  <template #default="{ refused }">
                    <BaseTrashButton
                      class="delete-unit"
                      data-test="delete-unit"
                      :disabled="refused"
                      @click="askToDelete(choice)"
                    />
                  </template>
                </BaseActionWithRefusalReason>
              </template>
            </v-chip>
          </v-chip-group>
          <v-chip
            class="new-unit"
            data-test="new-unit"
            prepend-icon="mdi-plus"
            @click="unitSession.openForCreate"
          >
            {{ t('admin.units.actions.new') }}
          </v-chip>
        </div>
      </v-defaults-provider>
      <BaseRefusalAlert
        v-if="unitRefusalText !== null && !isUnitDialogOpen"
        class="mt-2"
        data-test="unit-refusal"
        :text="unitRefusalText"
      />
    </div>
  </BaseFormDialog>
  <UnitDialog
    v-if="isUnitDialogOpen"
    :key="editedUnit?.unitId ?? 'new'"
    :unit="editedUnit"
    :error-text="unitRefusalText"
    @save="unitSession.save"
    @cancel="unitSession.close"
  />
  <BaseConfirmDialog
    v-if="unitAwaitingDeleteConfirmation !== null"
    :title="t('admin.units.labels.deleteTitle')"
    :body="t('admin.units.messages.deleteBody', { name: unitAwaitingDeleteConfirmation.name })"
    :confirm-label="t('admin.units.actions.deleteConfirm')"
    @confirm="deleteTheUnitAwaitingConfirmation"
    @cancel="unitAwaitingDeleteConfirmation = null"
  />
</template>
