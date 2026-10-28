<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminUnitView } from '../../../shared/api/generatedSchemas'
import { useAdminUnitsStore, type UnitDraft } from '../../stores/units'
import { useEditSession } from '../../composables/useEditSession'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseHeadingWithCheckbox from '../BaseHeadingWithCheckbox.vue'
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

const showsNoneYet = computed(
  () => units.units.length === 0 && !units.loadFailed,
)

const showsRefusal = computed(
  () => refusalText.value !== null && !isUnitDialogOpen.value,
)

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
      class="admin-heading mb-4"
      :checkbox-label="t('admin.common.actions.showDeactivated')"
      checkbox-test-name="show-deactivated"
    >
      <h1 class="text-h5">{{ t('admin.units.title') }}</h1>
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
      {{ t('admin.units.messages.noneYet') }}
    </p>

    <v-card
      v-for="row in rows"
      :key="row.unit.unitId"
      class="unit-row mb-2"
      data-test="unit-row"
      :data-test-id="row.unit.unitId"
    >
      <div class="d-flex align-center flex-wrap ga-2 px-4 py-2">
        <span class="name text-h6" data-test="name">{{ row.unit.name }}</span>
        <v-chip
          v-if="!row.unit.isActive"
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
      </div>
    </v-card>

    <v-btn
      class="new-unit mt-6"
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
