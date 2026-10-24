<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminAreaView } from '../../../shared/api/generatedSchemas'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import { assertNever } from '../../../shared/core/assertNever'
import { sortOrderForANewArea, type AreaMove } from '../../core/areaOrder'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import { useRowRefusals } from '../../composables/useRowRefusals'
import { useTypedNames } from '../../composables/useTypedNames'
import { useAdminTablePlanStore } from '../../stores/tablePlan'

const AREA_NAME_MISSING_KEY = 'errors.admin.festivals.areaNameMissing'

const props = defineProps<{ festivalId: string; areas: AdminAreaView[] }>()
const selectedAreaId = defineModel<string | null>('selectedAreaId', { required: true })

const { t } = useI18n()
const tablePlan = useAdminTablePlanStore()
const areaRefusals = useRowRefusals()
const typedNames = useTypedNames()
const { refusal: creationRefusal, refusalText: creationRefusalText } = useRefusalDisplay()
const newAreaName = ref('')
const areaAwaitingConfirmation = ref<AdminAreaView | null>(null)

function typeTheName(area: AdminAreaView, typed: string): void {
  areaRefusals.forgetRefusalOf(area.areaId)
  typedNames.type(area.areaId, area.name, typed)
}

async function rename(area: AdminAreaView): Promise<void> {
  const name = typedNames.takeNameToSend(area.areaId, area.name)
  if (name === null) {
    return
  }
  areaRefusals.forgetRefusalOf(area.areaId)
  if (name.trim().length === 0) {
    areaRefusals.refuse(area.areaId, AREA_NAME_MISSING_KEY)
    return
  }
  const saved = await tablePlan.saveArea(props.festivalId, area.areaId, { name, sortOrder: area.sortOrder })
  switch (saved.kind) {
    case 'ok':
      typedNames.forgetTypedName(area.areaId)
      return
    case 'failed':
      areaRefusals.showRefusalOf(area.areaId, saved)
      return
    default:
      assertNever(saved)
  }
}

async function move(area: AdminAreaView, direction: AreaMove): Promise<void> {
  areaRefusals.forgetRefusalOf(area.areaId)
  areaRefusals.showRefusalOf(area.areaId, await tablePlan.moveArea(props.festivalId, area.areaId, direction))
}

async function hide(area: AdminAreaView): Promise<void> {
  areaRefusals.forgetRefusalOf(area.areaId)
  areaRefusals.showRefusalOf(area.areaId, await tablePlan.hideArea(props.festivalId, area.areaId))
}

async function show(area: AdminAreaView): Promise<void> {
  areaRefusals.forgetRefusalOf(area.areaId)
  areaRefusals.showRefusalOf(area.areaId, await tablePlan.showArea(props.festivalId, area.areaId))
}

async function remove(): Promise<void> {
  const area = areaAwaitingConfirmation.value
  areaAwaitingConfirmation.value = null
  if (area === null) {
    return
  }
  areaRefusals.forgetRefusalOf(area.areaId)
  areaRefusals.showRefusalOf(area.areaId, await tablePlan.deleteArea(props.festivalId, area.areaId))
}

async function create(): Promise<void> {
  if (newAreaName.value.trim().length === 0) {
    return
  }
  creationRefusal.value = null
  const created = await tablePlan.createArea(props.festivalId, {
    name: newAreaName.value,
    sortOrder: sortOrderForANewArea(props.areas),
  })
  switch (created.kind) {
    case 'ok':
      newAreaName.value = ''
      selectedAreaId.value = created.value.areaId
      return
    case 'failed':
      creationRefusal.value = created.message
      return
    default:
      assertNever(created)
  }
}
</script>

<template>
  <section class="table-plan-areas" data-test="table-plan-areas">
    <h2 class="section-heading text-h6 mb-2">{{ t('admin.tablePlan.labels.areas') }}</h2>
    <div
      v-for="(area, position) in areas"
      :key="area.areaId"
      class="area-row mb-1"
      data-test="area-row"
      :data-test-id="area.areaId"
      :class="{ 'selected-row': area.areaId === selectedAreaId }"
    >
      <v-btn
        class="select-area justify-start"
        data-test="select-area"
        variant="text"
        block
        :class="{ 'text-medium-emphasis': area.isHidden }"
        @click="selectedAreaId = area.areaId"
      >
        <span class="area-name text-truncate" data-test="area-name">{{ area.name }}</span>
        <v-chip v-if="area.isHidden" class="hidden ml-2" data-test="hidden" size="small" color="grey">
          {{ t('admin.festivals.labels.hidden') }}
        </v-chip>
      </v-btn>

      <div v-if="area.areaId === selectedAreaId" class="area-controls px-2 pb-2">
        <v-text-field
          class="area-name-field mt-2"
          data-test="area-name-field"
          density="compact"
          hide-details
          :model-value="typedNames.nameShownFor(area.areaId, area.name)"
          :label="t('admin.tablePlan.labels.areaName')"
          @update:model-value="typeTheName(area, $event)"
          @blur="rename(area)"
          @keyup.enter="rename(area)"
        />
        <div class="d-flex align-center ga-1 mt-2">
          <v-btn
            class="move-area-up"
            data-test="move-area-up"
            icon="mdi-arrow-up"
            variant="text"
            size="small"
            :disabled="position === 0"
            @click="move(area, 'up')"
          />
          <v-btn
            class="move-area-down"
            data-test="move-area-down"
            icon="mdi-arrow-down"
            variant="text"
            size="small"
            :disabled="position === areas.length - 1"
            @click="move(area, 'down')"
          />
          <v-spacer />
          <v-btn
            v-if="!area.isHidden"
            class="hide-area"
            data-test="hide-area"
            variant="text"
            size="small"
            @click="hide(area)"
          >
            {{ t('admin.festivals.actions.hide') }}
          </v-btn>
          <v-btn
            v-else
            class="show-area"
            data-test="show-area"
            variant="text"
            size="small"
            @click="show(area)"
          >
            {{ t('admin.festivals.actions.show') }}
          </v-btn>
          <v-btn
            class="delete-area"
            data-test="delete-area"
            icon="mdi-delete"
            variant="text"
            size="small"
            color="error"
            @click="areaAwaitingConfirmation = area"
          />
        </div>
      </div>

      <v-alert
        v-if="areaRefusals.refusalTextOf(area.areaId) !== null"
        class="refusal mx-2 mb-2"
        data-test="refusal"
        type="warning"
        variant="tonal"
      >
        {{ areaRefusals.refusalTextOf(area.areaId) }}
      </v-alert>
    </div>

    <div class="create-area mt-4">
      <v-text-field
        v-model="newAreaName"
        class="new-area-name"
        data-test="new-area-name"
        density="compact"
        hide-details
        :label="t('admin.tablePlan.labels.newAreaName')"
        @update:model-value="creationRefusal = null"
        @keyup.enter="create"
      />
      <v-btn
        class="create-area-button mt-2"
        data-test="create-area"
        color="primary"
        variant="tonal"
        block
        :disabled="newAreaName.trim().length === 0"
        @click="create"
      >
        {{ t('admin.tablePlan.actions.createArea') }}
      </v-btn>
      <v-alert
        v-if="creationRefusalText !== null"
        class="refusal mt-2"
        data-test="refusal"
        type="warning"
        variant="tonal"
      >
        {{ creationRefusalText }}
      </v-alert>
    </div>

    <BaseConfirmDialog
      v-if="areaAwaitingConfirmation !== null"
      :title="t('admin.tablePlan.labels.deleteAreaTitle', { name: areaAwaitingConfirmation.name })"
      :body="t('admin.tablePlan.messages.deleteAreaBody')"
      :confirm-label="t('admin.tablePlan.actions.delete')"
      @confirm="remove"
      @cancel="areaAwaitingConfirmation = null"
    />
  </section>
</template>

<style scoped>
.area-row.selected-row {
  background-color: rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 6px;
}
</style>
