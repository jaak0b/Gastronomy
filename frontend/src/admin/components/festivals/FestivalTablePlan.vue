<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { navigate } from '../../../shared/router/router'
import { useAdminTablePlanStore } from '../../stores/tablePlan'

defineProps<{ festivalId: string }>()

const { t } = useI18n()
const tablePlan = useAdminTablePlanStore()

const shownAreas = computed(() => tablePlan.areas.filter((area) => !area.isHidden))
const hasNoShownArea = computed(() => !tablePlan.loadFailed && shownAreas.value.length === 0)
const hasNoShownTable = computed(
  () =>
    !tablePlan.loadFailed
    && shownAreas.value.every((area) => area.tables.every((table) => table.isHidden)),
)
</script>

<template>
  <section class="festival-table-plan mb-4" data-test="festival-table-plan">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3" data-test="section-heading">{{ t('admin.tablePlan.title') }}</h2>

        <div v-if="tablePlan.areas.length > 0" class="rows mb-3">
          <div
            v-for="(area, position) in tablePlan.areas"
            :key="area.areaId"
            class="festival-area-row"
            data-test="festival-area-row"
            :data-test-id="area.areaId"
            :class="{ 'tinted-row': position % 2 === 1 }"
          >
            <div class="row-line d-flex align-center flex-wrap ga-3 py-2 px-3">
              <span class="name text-body-1" data-test="area-name">{{ area.name }}</span>
              <v-chip v-if="area.isHidden" class="hidden" data-test="hidden" size="small" color="grey">
                {{ t('admin.festivals.labels.hidden') }}
              </v-chip>
              <v-spacer />
              <span class="table-count text-body-2 text-medium-emphasis" data-test="table-count">
                {{ t('admin.tablePlan.labels.tableCount', { count: area.tables.length }, area.tables.length) }}
              </span>
            </div>
          </div>
        </div>

        <v-alert v-if="hasNoShownArea" class="no-area mb-3" data-test="no-area" type="warning" variant="tonal">
          {{ t('admin.tablePlan.messages.noArea') }}
        </v-alert>
        <v-alert v-else-if="hasNoShownTable" class="no-table mb-3" data-test="no-table" type="warning" variant="tonal">
          {{ t('admin.tablePlan.messages.noTable') }}
        </v-alert>

        <v-btn
          class="edit-table-plan"
          data-test="edit-table-plan"
          color="primary"
          variant="tonal"
          @click="navigate(`/admin/festivals/${festivalId}/table-plan`)"
        >
          {{ t('admin.tablePlan.actions.edit') }}
        </v-btn>
      </div>
    </v-card>
  </section>
</template>

<style scoped>
.festival-area-row + .festival-area-row {
  border-top: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
}

.festival-area-row.tinted-row {
  background-color: rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 6px;
}
</style>
