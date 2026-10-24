<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminAreaView, MoveDirection, SavedAreaView } from '../../../shared/api/generatedSchemas'
import { navigate } from '../../../shared/router/router'
import type { AdminActionResult } from '../../core/adminActionResult'
import { useAdminTablePlanStore } from '../../stores/tablePlan'
import HideableEntryList from '../HideableEntryList.vue'
import AreaDialog from '../tablePlan/AreaDialog.vue'

interface AreaDraft {
  name: string
}

const props = defineProps<{ festivalId: string; isRunning: boolean }>()

const { t } = useI18n()
const tablePlan = useAdminTablePlanStore()

const hasLoadedThePlan = computed(
  () => tablePlan.loadedFestivalId === props.festivalId && !tablePlan.loadFailed,
)
const shownAreas = computed(() => tablePlan.areas.filter((area) => !area.isHidden))
const shownTableCounts = computed(
  () => new Map(tablePlan.areas.map((area) => [area.areaId, area.tables.filter((table) => !table.isHidden).length])),
)
const hasNoArea = computed(() => hasLoadedThePlan.value && tablePlan.areas.length === 0)
const hasOnlyHiddenAreas = computed(
  () => hasLoadedThePlan.value && tablePlan.areas.length > 0 && shownAreas.value.length === 0,
)
const hasNoShownTable = computed(
  () =>
    hasLoadedThePlan.value
    && shownAreas.value.length > 0
    && shownAreas.value.every((area) => area.tables.every((table) => table.isHidden)),
)

async function saveTheArea(
  draft: AreaDraft,
  area: AdminAreaView | null,
): Promise<AdminActionResult<SavedAreaView | null>> {
  return area === null
    ? await tablePlan.createArea(props.festivalId, { name: draft.name })
    : await tablePlan.saveArea(props.festivalId, area.areaId, { name: draft.name })
}

async function moveTheArea(
  area: AdminAreaView,
  direction: MoveDirection,
  showsHiddenAreas: boolean,
): Promise<AdminActionResult<null>> {
  return await tablePlan.moveArea(props.festivalId, area.areaId, { direction, showsHiddenAreas })
}

function hidingRefusalOf(area: AdminAreaView): string | null {
  return props.isRunning && area.hasItemsToHandOutOrSettle
    ? t('errors.admin.festivals.areaWithItemsToHandOutOrSettleStaysShown')
    : null
}
</script>

<template>
  <section class="festival-table-plan mb-4" data-test="festival-table-plan">
    <v-card variant="outlined">
      <div class="pa-4">
        <HideableEntryList
          :entries="tablePlan.areas"
          :id-of="(area: AdminAreaView) => area.areaId"
          :name-of="(area: AdminAreaView) => area.name"
          :is-hidden="(area: AdminAreaView) => area.isHidden"
          :hiding-refusal-of="hidingRefusalOf"
          row-test-name="festival-area-row"
          :new-label="t('admin.tablePlan.actions.newArea')"
          :hide-title="t('admin.tablePlan.labels.hideAreaTitle')"
          :hide-body="t('admin.tablePlan.messages.hideAreaBody')"
          :save-through="saveTheArea"
          :hide-through="(area: AdminAreaView) => tablePlan.hideArea(festivalId, area.areaId)"
          :show-through="(area: AdminAreaView) => tablePlan.showArea(festivalId, area.areaId)"
          :move-through="moveTheArea"
        >
          <template #heading>
            <h2 class="section-heading text-h6" data-test="section-heading">{{ t('admin.tablePlan.title') }}</h2>
          </template>
          <template #notices>
            <v-alert v-if="hasNoArea" class="no-area mb-3" data-test="no-area" type="warning" variant="tonal">
              {{ t('admin.tablePlan.messages.noArea') }}
            </v-alert>
            <v-alert
              v-else-if="hasOnlyHiddenAreas"
              class="all-areas-hidden mb-3"
              data-test="all-areas-hidden"
              type="warning"
              variant="tonal"
            >
              {{ t('admin.tablePlan.messages.allAreasHidden') }}
            </v-alert>
            <v-alert v-else-if="hasNoShownTable" class="no-table mb-3" data-test="no-table" type="warning" variant="tonal">
              {{ t('admin.tablePlan.messages.noTable') }}
            </v-alert>
          </template>
          <template #details="{ entry: area }">
            <span class="table-count text-body-2 text-medium-emphasis" data-test="table-count">
              {{
                t(
                  'admin.tablePlan.labels.tableCount',
                  { count: shownTableCounts.get(area.areaId) },
                  shownTableCounts.get(area.areaId) ?? 0,
                )
              }}
            </span>
            <v-btn
              class="open-plan"
              data-test="open-plan"
              color="primary"
              variant="tonal"
              @click="navigate(`/admin/festivals/${festivalId}/areas/${area.areaId}`)"
            >
              {{ t('admin.tablePlan.actions.openPlan') }}
            </v-btn>
          </template>
          <template #dialog="{ entry: area, errorText, save, cancel }">
            <AreaDialog :area="area" :error-text="errorText" @save="save" @cancel="cancel" />
          </template>
        </HideableEntryList>
      </div>
    </v-card>
  </section>
</template>
