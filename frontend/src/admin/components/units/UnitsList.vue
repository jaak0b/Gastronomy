<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminUnitView } from '../../../shared/api/generatedSchemas'
import { showsTheEmptyListText } from '../../../shared/core/emptyList'
import { useAdminUnitsStore, type UnitDraft } from '../../stores/units'
import { useEditSession } from '../../composables/useEditSession'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import BaseEmptyListText from '../../../shared/components/BaseEmptyListText.vue'
import BaseRefusalAlert from '../../../shared/components/BaseRefusalAlert.vue'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseHeadingWithCheckbox from '../BaseHeadingWithCheckbox.vue'
import BaseListRow from '../BaseListRow.vue'
import BaseShowButton from '../BaseShowButton.vue'
import BaseTrashButton from '../BaseTrashButton.vue'
import UnitDialog from './UnitDialog.vue'

interface UnitRow {
  unit: AdminUnitView
  deactivationRefusal: string | null
}

const { t } = useI18n()
const units = useAdminUnitsStore()
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const unitSession = useEditSession<AdminUnitView, UnitDraft, AdminUnitView | null>({
  saveThrough: (draft, unit) =>
    unit === null
      ? units.create(draft)
      : units.save(unit.unitId, draft),
})
const {
  isOpen: isUnitDialogOpen,
  edited: editedUnit,
  refusalText,
} = unitSession
let stopListening: (() => void) | null = null

const rows = computed<UnitRow[]>(() =>
  units.units
    .filter((unit) => showsDeactivated.value || unit.isActive)
    .map((unit) => ({
      unit,
      deactivationRefusal:
        unit.usedByIngredientNames.length > 0
          ? t('admin.units.messages.usedByIngredients', {
              ingredients: unit.usedByIngredientNames.join(', '),
            })
          : null,
    })),
)

const showsEmptyListText = computed(() => showsTheEmptyListText(units, units.units.length))

async function deactivate(): Promise<void> {
  const unitId = askingAboutId.value
  askingAboutId.value = null
  if (unitId !== null) {
    unitSession.forgetRefusal()
    unitSession.showRefusalOf(await units.setActive(unitId, false))
  }
}

async function reactivate(unitId: string): Promise<void> {
  unitSession.forgetRefusal()
  unitSession.showRefusalOf(await units.setActive(unitId, true))
}

onMounted(async () => {
  stopListening = units.listen()
  await units.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-units" data-test="admin-units">
    <BaseHeadingWithCheckbox
      v-model="showsDeactivated"
      class="admin-heading admin-section-gap"
      :checkbox-label="t('admin.common.actions.showDeactivated')"
      checkbox-test-name="show-deactivated"
    >
      <h1 class="app-page-title">{{ t('admin.units.title') }}</h1>
    </BaseHeadingWithCheckbox>

    <BaseRefusalAlert
      v-if="refusalText !== null && !isUnitDialogOpen"
      class="admin-section-gap"
      :text="refusalText"
    />

    <BaseEmptyListText v-if="showsEmptyListText" class="admin-section-gap" />

    <BaseListRow
      v-for="(row, position) in rows"
      :key="row.unit.unitId"
      class="unit-row"
      data-test="unit-row"
      :data-test-id="row.unit.unitId"
      :position="position"
      :name="row.unit.name"
      name-test-name="name"
    >
      <template #facts>
        <v-chip
          v-if="!row.unit.isActive"
          class="deactivated"
          data-test="deactivated"
          color="grey"
        >
          {{ t('admin.common.labels.deactivated') }}
        </v-chip>
      </template>
      <template #actions>
        <BaseEditButton
          class="edit"
          data-test="edit"
          @click="unitSession.openForEdit(row.unit)"
        />
        <BaseActionWithRefusalReason
          v-if="row.unit.isActive"
          class="deactivate-wrapper"
          data-test="deactivate-wrapper"
          :refusal-reason="row.deactivationRefusal"
        >
          <template #default="{ refused }">
            <BaseTrashButton
              class="deactivate"
              data-test="deactivate"
              :disabled="refused"
              @click="askingAboutId = row.unit.unitId"
            />
          </template>
        </BaseActionWithRefusalReason>
        <BaseShowButton
          v-else
          class="reactivate"
          data-test="reactivate"
          :label="t('admin.units.actions.activate')"
          @click="reactivate(row.unit.unitId)"
        />
      </template>
    </BaseListRow>

    <v-btn
      class="new-unit admin-block-gap"
      data-test="new-unit"
      color="primary"
      @click="unitSession.openForCreate"
    >
      {{ t('admin.units.actions.new') }}
    </v-btn>

    <UnitDialog
      v-if="isUnitDialogOpen"
      :key="editedUnit?.unitId ?? 'new'"
      :unit="editedUnit"
      :error-text="refusalText"
      @save="unitSession.save"
      @cancel="unitSession.close"
    />
    <BaseConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.units.labels.deactivateTitle')"
      :confirm-label="t('admin.units.actions.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
  </v-container>
</template>
