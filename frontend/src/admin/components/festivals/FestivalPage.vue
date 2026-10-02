<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { navigate } from '../../../shared/router/router'
import { assertNever } from '../../../shared/core/assertNever'
import { combineReleases } from '../../../shared/core/combineReleases'
import { localInputToUtcIso, utcIsoToLocalInput } from '../../core/festivalTimes'
import { laptopConfirmedField, stillDiffersFromTheLaptop } from '../../core/laptopConfirmedField'
import { useAdminCategoriesStore } from '../../stores/categories'
import { useAdminFestivalStockStore } from '../../stores/festivalStock'
import { useAdminFestivalsStore } from '../../stores/festivals'
import { useAdminItemsStore } from '../../stores/items'
import { useAdminStationsStore } from '../../stores/stations'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import FestivalItems from './FestivalItems.vue'
import FestivalStations from './FestivalStations.vue'
import FestivalStock from './FestivalStock.vue'

const props = defineProps<{ festivalId: string }>()

const { t } = useI18n()
const festivals = useAdminFestivalsStore()
const stations = useAdminStationsStore()
const categories = useAdminCategoriesStore()
const items = useAdminItemsStore()
const stock = useAdminFestivalStockStore()
const festivalName = ref(laptopConfirmedField('', ''))
const startsAt = ref(laptopConfirmedField('', ''))
const endsAt = ref(laptopConfirmedField('', ''))
const refusedField = ref<'name' | 'period' | null>(null)
const { refusal, refusalText } = useRefusalDisplay()
let stopListening: (() => void) | null = null

const festival = computed(() => festivals.findFestivalWithId(props.festivalId))

watch(
  festival,
  (shown) => {
    if (shown === null) {
      return
    }
    festivalName.value = laptopConfirmedField(shown.name, shown.name)
    startsAt.value = laptopConfirmedField(utcIsoToLocalInput(shown.startsAtUtc), shown.startsAtUtc)
    endsAt.value = laptopConfirmedField(utcIsoToLocalInput(shown.endsAtUtc), shown.endsAtUtc)
  },
  { immediate: true },
)

function namesTheSameMoment(editedLocalInput: string, confirmedUtcIso: string): boolean {
  const editedUtcIso = localInputToUtcIso(editedLocalInput)
  return (
    editedUtcIso !== null
    && new Date(editedUtcIso).getTime() === new Date(confirmedUtcIso).getTime()
  )
}

async function saveTheFields(): Promise<void> {
  if (festival.value === null) {
    return
  }
  const startsAtUtc = localInputToUtcIso(startsAt.value.edited)
  const endsAtUtc = localInputToUtcIso(endsAt.value.edited)
  const alreadyAtTheLaptop =
    !stillDiffersFromTheLaptop(festivalName.value)
    && !stillDiffersFromTheLaptop(startsAt.value, namesTheSameMoment)
    && !stillDiffersFromTheLaptop(endsAt.value, namesTheSameMoment)
  if (alreadyAtTheLaptop) {
    return
  }
  if (festivalName.value.edited.trim().length === 0) {
    refusedField.value = 'name'
    return
  }
  if (startsAtUtc === null || endsAtUtc === null) {
    refusedField.value = 'period'
    return
  }
  refusedField.value = null
  refusal.value = null
  festivalName.value.confirmedByTheLaptop = festivalName.value.edited
  startsAt.value.confirmedByTheLaptop = startsAtUtc
  endsAt.value.confirmedByTheLaptop = endsAtUtc
  const saved = await festivals.save(props.festivalId, {
    name: festivalName.value.edited,
    startsAtUtc,
    endsAtUtc,
  })
  switch (saved.kind) {
    case 'ok':
      return
    case 'failed':
      refusal.value = saved.message
      return
    default:
      assertNever(saved)
  }
}

async function reload(): Promise<void> {
  await festivals.load()
  if (festival.value === null) {
    if (!festivals.loadFailed) {
      navigate('/admin/overview')
    }
    return
  }
  await stations.loadAtTheFestival(props.festivalId)
  await categories.load()
  await items.loadAtTheFestival(props.festivalId)
  await stock.loadForFestival(props.festivalId)
}

watch(
  () => props.festivalId,
  () => {
    void reload()
  },
)

onMounted(async () => {
  stopListening = combineReleases(
    festivals.listen(),
    stations.listen(),
    categories.listen(),
    items.listen(),
    stock.listen(),
  )
  await reload()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-festival">
    <v-btn
      class="back-to-festivals mb-2"
      variant="text"
      prepend-icon="mdi-arrow-left"
      @click="navigate('/admin/festivals')"
    >
      {{ t('common.actions.back') }}
    </v-btn>
    <v-alert
      v-if="festivals.loadFailed || stock.loadFailed"
      class="error mb-4"
      type="error"
      variant="tonal"
    >
      {{ t('admin.common.errors.loadFailed') }}
    </v-alert>

    <template v-if="festival !== null">
      <v-card class="festival-fields mb-4" variant="outlined">
        <div class="pa-4">
          <div class="festival-heading d-flex align-center flex-wrap ga-3 mb-4">
            <h1 class="festival-name text-h5">{{ festival.name }}</h1>
            <v-chip v-if="festival.isRunning" class="running" size="small" color="success">
              {{ t('admin.festivals.labels.running') }}
            </v-chip>
          </div>

          <div class="d-flex align-start flex-wrap ga-4">
            <v-text-field
              v-model="festivalName.edited"
              class="festival-name-field flex-grow-1"
              maxlength="80"
              density="compact"
              hide-details="auto"
              :error-messages="
                refusedField === 'name' ? [t('errors.admin.festivals.nameMissing')] : []
              "
              :label="t('admin.festivals.labels.name')"
              @blur="saveTheFields"
              @keyup.enter="saveTheFields"
            />
            <v-text-field
              v-model="startsAt.edited"
              class="festival-start-field"
              type="datetime-local"
              density="compact"
              hide-details
              :label="t('admin.festivals.labels.start')"
              @blur="saveTheFields"
              @keyup.enter="saveTheFields"
            />
            <v-text-field
              v-model="endsAt.edited"
              class="festival-end-field"
              type="datetime-local"
              density="compact"
              hide-details
              :label="t('admin.festivals.labels.end')"
              @blur="saveTheFields"
              @keyup.enter="saveTheFields"
            />
          </div>
          <p v-if="refusedField === 'period'" class="period-refusal text-error text-body-2 mt-2">
            {{ t('errors.admin.festivals.periodInvalid') }}
          </p>
          <v-alert v-if="refusalText !== null" class="refusal mt-3" type="warning" variant="tonal">
            {{ refusalText }}
          </v-alert>
        </div>
      </v-card>

      <FestivalStations :festival-id="festivalId" />
      <FestivalItems :festival-id="festivalId" :is-running="festival?.isRunning === true" />
      <FestivalStock :festival-id="festivalId" />
    </template>
  </v-container>
</template>

<style scoped>
.admin-festival {
  max-width: 1100px;
}

.festival-start-field,
.festival-end-field {
  flex: 0 0 14rem;
}
</style>
