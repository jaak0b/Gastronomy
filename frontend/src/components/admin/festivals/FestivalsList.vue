<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { navigate } from '../../../router'
import { formatFestivalMoment } from '../../../core/festivalTimes'
import {
  useAdminFestivalsStore,
  type AdminFestival,
  type FestivalDraft,
} from '../../../stores/admin/festivals'
import ConfirmDialog from '../ConfirmDialog.vue'
import { useRefusalText } from '../refusalText'
import FestivalForm from './FestivalForm.vue'

const { t, locale } = useI18n()
const festivals = useAdminFestivalsStore()
const showsHidden = ref(false)
const isCreating = ref(false)
const copiedFestival = ref<AdminFestival | null>(null)
const hiddenFestival = ref<AdminFestival | null>(null)
let stopListening: (() => void) | null = null

const refusalText = useRefusalText([() => festivals.errorMessage])

const shown = computed(() =>
  festivals.festivals.filter((festival) => showsHidden.value || !festival.isHidden),
)

function moment(value: string): string {
  return formatFestivalMoment(value, locale.value)
}

function closeTheForms(): void {
  isCreating.value = false
  copiedFestival.value = null
  festivals.forgetError()
}

function startCreating(): void {
  closeTheForms()
  isCreating.value = true
}

function startCopying(festival: AdminFestival): void {
  closeTheForms()
  copiedFestival.value = festival
}

function open(festival: AdminFestival): void {
  navigate(`/admin/festivals/${festival.festivalId}`)
}

async function create(draft: FestivalDraft): Promise<void> {
  if (await festivals.create(draft)) {
    closeTheForms()
  }
}

async function copy(draft: FestivalDraft): Promise<void> {
  const festival = copiedFestival.value
  if (festival !== null && (await festivals.copy(festival.festivalId, draft))) {
    closeTheForms()
  }
}

async function hide(): Promise<void> {
  const festival = hiddenFestival.value
  hiddenFestival.value = null
  if (festival !== null) {
    await festivals.hide(festival.festivalId)
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
    <h1 class="text-h5 mb-4">{{ t('admin.festivals.title') }}</h1>

    <v-alert v-if="refusalText !== null" class="refusal mb-4" type="warning" variant="tonal">
      {{ refusalText }}
    </v-alert>
    <v-alert v-if="festivals.loadFailed" class="error mb-4" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>

    <v-checkbox
      v-model="showsHidden"
      class="show-hidden"
      :label="t('admin.festivals.showHidden')"
    />

    <p v-if="shown.length === 0" class="none-yet text-medium-emphasis mb-4">
      {{ t('admin.festivals.noneYet') }}
    </p>

    <v-card
      v-for="festival in shown"
      :key="festival.festivalId"
      class="festival-row mb-2"
    >
      <div class="d-flex align-center flex-wrap ga-2 px-4 py-2">
        <span class="name text-h6">{{ festival.name }}</span>
        <v-chip v-if="festival.isRunning" class="running" size="small" color="success">
          {{ t('admin.festivals.running') }}
        </v-chip>
        <v-chip v-if="festival.isHidden" class="hidden" size="small" color="grey">
          {{ t('admin.festivals.hidden') }}
        </v-chip>
        <v-spacer />
        <div class="actions d-flex align-center ga-2">
          <v-btn class="open" color="primary" variant="tonal" @click="open(festival)">
            {{ t('admin.festivals.edit') }}
          </v-btn>
          <v-btn class="copy" variant="text" @click="startCopying(festival)">
            {{ t('admin.festivals.copy') }}
          </v-btn>
          <span class="conditional-action">
            <v-btn
              v-if="!festival.isRunning && !festival.isHidden"
              class="hide"
              variant="text"
              @click="hiddenFestival = festival"
            >
              {{ t('admin.festivals.hide') }}
            </v-btn>
            <v-btn
              v-if="festival.isHidden"
              class="show"
              variant="text"
              @click="festivals.show(festival.festivalId)"
            >
              {{ t('admin.festivals.show') }}
            </v-btn>
            <v-btn class="action-measure" variant="text" tabindex="-1" aria-hidden="true">
              <span class="measure-labels">
                <span>{{ t('admin.festivals.hide') }}</span>
                <span>{{ t('admin.festivals.show') }}</span>
              </span>
            </v-btn>
          </span>
        </div>
      </div>
      <div class="festival-facts d-flex flex-wrap ga-4 px-4 pb-3 text-medium-emphasis">
        <span class="period-start">
          {{ t('admin.festivals.start') }}: {{ moment(festival.startsAtUtc) }}
        </span>
        <span class="period-end">
          {{ t('admin.festivals.end') }}: {{ moment(festival.endsAtUtc) }}
        </span>
        <span class="station-count">
          {{ t('admin.festivals.stationCount', { count: festival.stationCount }, festival.stationCount) }}
        </span>
        <span class="menu-item-count">
          {{ t('admin.festivals.menuItemCount', { count: festival.menuItemCount }, festival.menuItemCount) }}
        </span>
        <span class="order-count">
          {{ t('admin.festivals.orderCount', { count: festival.orderCount }, festival.orderCount) }}
        </span>
      </div>
    </v-card>

    <v-btn class="new-festival" color="primary" @click="startCreating">
      {{ t('admin.festivals.new') }}
    </v-btn>

    <FestivalForm
      v-if="isCreating"
      :festival="null"
      :title="t('admin.festivals.new')"
      :confirm-label="t('admin.save')"
      :error-text="refusalText"
      @save="create"
      @cancel="closeTheForms"
    />
    <FestivalForm
      v-if="copiedFestival !== null"
      :key="copiedFestival.festivalId"
      :festival="null"
      :title="t('admin.festivals.copyTitle')"
      :confirm-label="t('admin.festivals.copyConfirm')"
      :error-text="refusalText"
      @save="copy"
      @cancel="closeTheForms"
    />
    <ConfirmDialog
      v-if="hiddenFestival !== null"
      :title="t('admin.festivals.hideTitle')"
      :body="t('admin.festivals.hideBody')"
      :confirm-label="t('admin.festivals.hideConfirm')"
      @confirm="hide"
      @cancel="hiddenFestival = null"
    />
  </v-container>
</template>

<style scoped>
.actions {
  flex: 0 0 auto;
}

.conditional-action {
  display: grid;
  flex: 0 0 auto;
}

.conditional-action > * {
  grid-area: 1 / 1;
}

.action-measure {
  visibility: hidden;
}

.measure-labels {
  display: grid;
}

.measure-labels > * {
  grid-area: 1 / 1;
}
</style>
