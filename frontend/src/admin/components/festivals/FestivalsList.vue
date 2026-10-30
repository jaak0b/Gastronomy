<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { navigate } from '../../../shared/router/router'
import { AdminFestivalView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { showsTheEmptyListText } from '../../../shared/core/emptyList'
import { formatFestivalMoment } from '../../../shared/core/moments'
import { useAdminFestivalsStore, type FestivalDraft } from '../../stores/festivals'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseHideConfirmDialog from '../BaseHideConfirmDialog.vue'
import BaseHeadingWithCheckbox from '../BaseHeadingWithCheckbox.vue'
import BaseListRow from '../BaseListRow.vue'
import BaseEmptyListText from '../../../shared/components/BaseEmptyListText.vue'
import BaseIconAction from '../../../shared/components/BaseIconAction.vue'
import BaseRefusalAlert from '../../../shared/components/BaseRefusalAlert.vue'
import BaseShowButton from '../BaseShowButton.vue'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import FestivalDialog from './FestivalDialog.vue'

const { t, locale } = useI18n()
const festivals = useAdminFestivalsStore()
const showsHidden = ref(false)
const isCreating = ref(false)
const copiedFestival = ref<AdminFestivalView | null>(null)
const hiddenFestival = ref<AdminFestivalView | null>(null)
const { refusal, refusalText } = useRefusalDisplay()
let stopListening: (() => void) | null = null

const shown = computed(() =>
  festivals.festivals.filter((festival) => showsHidden.value || !festival.isHidden),
)

const showsEmptyListText = computed(() =>
  showsTheEmptyListText(festivals, festivals.festivals.length),
)

function moment(value: string): string {
  return formatFestivalMoment(value, locale.value)
}

function closeTheForms(): void {
  isCreating.value = false
  copiedFestival.value = null
  refusal.value = null
}

function startCreating(): void {
  closeTheForms()
  isCreating.value = true
}

function startCopying(festival: AdminFestivalView): void {
  closeTheForms()
  copiedFestival.value = festival
}

function open(festival: AdminFestivalView): void {
  navigate(`/admin/festivals/${festival.festivalId}`)
}

async function create(draft: FestivalDraft): Promise<void> {
  refusal.value = null
  const created = await festivals.create(draft)
  switch (created.kind) {
    case 'ok':
      closeTheForms()
      return
    case 'failed':
      refusal.value = created.message
      return
    default:
      assertNever(created)
  }
}

async function copy(draft: FestivalDraft): Promise<void> {
  const festival = copiedFestival.value
  if (festival === null) {
    return
  }
  refusal.value = null
  const copied = await festivals.copy(festival.festivalId, draft)
  switch (copied.kind) {
    case 'ok':
      closeTheForms()
      return
    case 'failed':
      refusal.value = copied.message
      return
    default:
      assertNever(copied)
  }
}

async function hide(): Promise<void> {
  const festival = hiddenFestival.value
  hiddenFestival.value = null
  if (festival === null) {
    return
  }
  refusal.value = null
  const hidden = await festivals.hide(festival.festivalId)
  switch (hidden.kind) {
    case 'ok':
      return
    case 'failed':
      refusal.value = hidden.message
      return
    default:
      assertNever(hidden)
  }
}

async function show(festivalId: string): Promise<void> {
  refusal.value = null
  const shownFestival = await festivals.show(festivalId)
  switch (shownFestival.kind) {
    case 'ok':
      return
    case 'failed':
      refusal.value = shownFestival.message
      return
    default:
      assertNever(shownFestival)
  }
}

onMounted(async () => {
  stopListening = festivals.listen()
  await festivals.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-festivals">
    <BaseHeadingWithCheckbox
      v-model="showsHidden"
      class="admin-heading admin-section-gap"
      :checkbox-label="t('admin.festivals.actions.showHidden')"
      checkbox-test-name="show-hidden"
    >
      <h1 class="app-page-title">{{ t('admin.festivals.title') }}</h1>
    </BaseHeadingWithCheckbox>

    <BaseRefusalAlert
      v-if="refusalText !== null && !isCreating && copiedFestival === null"
      class="admin-section-gap"
      :text="refusalText"
    />

    <BaseEmptyListText v-if="showsEmptyListText" class="admin-section-gap" />

    <BaseListRow
      v-for="(festival, position) in shown"
      :key="festival.festivalId"
      class="festival-row"
      data-test="festival-row"
      :data-test-id="festival.festivalId"
      :position="position"
      :name="festival.name"
      name-test-name="name"
    >
      <template #facts>
        <v-chip v-if="festival.isRunning" class="running" data-test="running" color="success">
          {{ t('admin.festivals.labels.running') }}
        </v-chip>
        <v-chip v-if="festival.isHidden" class="hidden" color="grey">
          {{ t('admin.festivals.labels.hidden') }}
        </v-chip>
      </template>
      <template #actions>
        <div class="actions d-flex align-center ga-2" data-test="actions">
          <v-btn class="copy" data-test="copy" variant="text" @click="startCopying(festival)">
            {{ t('admin.festivals.actions.copy') }}
          </v-btn>
          <BaseEditButton class="open" data-test="open" @click="open(festival)" />
          <BaseShowButton
            v-if="festival.isHidden"
            class="show"
            data-test="show"
            :label="t('admin.festivals.actions.show')"
            @click="show(festival.festivalId)"
          />
          <BaseActionWithRefusalReason
            v-else
            class="hide-wrapper"
            data-test="hide-wrapper"
            :refusal-reason="festival.isRunning ? t('errors.admin.festivals.runningFestivalStaysShown') : null"
          >
            <template #default="{ refused }">
              <BaseIconAction
                class="hide"
                data-test="hide"
                icon="mdi-eye-off"
                color="error"
                :label="t('admin.festivals.actions.hide')"
                :disabled="refused"
                @click="hiddenFestival = festival"
              />
            </template>
          </BaseActionWithRefusalReason>
        </div>
      </template>
      <div class="festival-facts d-flex flex-wrap admin-row-details text-medium-emphasis">
        <span class="period-start">
          {{ t('admin.festivals.labels.start') }}: {{ moment(festival.startsAtUtc) }}
        </span>
        <span class="period-end">
          {{ t('admin.festivals.labels.end') }}: {{ moment(festival.endsAtUtc) }}
        </span>
        <span class="station-count" data-test="station-count">
          {{ t('admin.festivals.labels.stationCount', { count: festival.stationCount }, festival.stationCount) }}
        </span>
        <span class="menu-item-count" data-test="menu-item-count">
          {{ t('admin.festivals.labels.menuItemCount', { count: festival.menuItemCount }, festival.menuItemCount) }}
        </span>
        <span class="order-count" data-test="order-count">
          {{ t('admin.festivals.labels.orderCount', { count: festival.orderCount }, festival.orderCount) }}
        </span>
      </div>
    </BaseListRow>

    <v-btn class="new-festival admin-block-gap" data-test="new-festival" color="primary" @click="startCreating">
      {{ t('admin.festivals.actions.new') }}
    </v-btn>

    <FestivalDialog
      v-if="isCreating"
      :festival="null"
      :title="t('admin.festivals.actions.new')"
      :confirm-label="t('admin.common.actions.save')"
      :error-text="refusalText"
      @save="create"
      @cancel="closeTheForms"
    />
    <FestivalDialog
      v-if="copiedFestival !== null"
      :key="copiedFestival.festivalId"
      :festival="null"
      :title="t('admin.festivals.labels.copyTitle')"
      :confirm-label="t('admin.festivals.actions.copyConfirm')"
      :error-text="refusalText"
      @save="copy"
      @cancel="closeTheForms"
    />
    <BaseHideConfirmDialog
      v-if="hiddenFestival !== null"
      :title="t('admin.festivals.labels.hideTitle')"
      @confirm="hide"
      @cancel="hiddenFestival = null"
    />
  </v-container>
</template>

<style scoped>
.actions {
  flex: 0 0 auto;
}
</style>
