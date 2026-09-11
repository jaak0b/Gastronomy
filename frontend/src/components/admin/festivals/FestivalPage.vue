<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { navigate } from '../../../router'
import { localInputToUtcIso, utcIsoToLocalInput } from '../../../core/festivalTimes'
import { useAdminCategoriesStore } from '../../../stores/admin/categories'
import { useAdminFestivalsStore } from '../../../stores/admin/festivals'
import { useAdminItemsStore } from '../../../stores/admin/items'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import { useRefusalText } from '../refusalText'
import FestivalItems from './FestivalItems.vue'
import FestivalStations from './FestivalStations.vue'

const props = defineProps<{ festivalId: string }>()

const { t } = useI18n()
const festivals = useAdminFestivalsStore()
const stations = useAdminStationsStore()
const categories = useAdminCategoriesStore()
const items = useAdminItemsStore()
const name = ref('')
const startsAt = ref('')
const endsAt = ref('')
const refusedField = ref<'name' | 'period' | null>(null)
const sent = ref({ name: '', startsAtUtc: '', endsAtUtc: '' })
let stopListening: (() => void) | null = null

const festival = computed(() => festivals.festivalWithId(props.festivalId))
const refusalText = useRefusalText([() => festivals.errorMessage])

watch(
  festival,
  (shown) => {
    if (shown === null) {
      return
    }
    name.value = shown.name
    startsAt.value = utcIsoToLocalInput(shown.startsAtUtc)
    endsAt.value = utcIsoToLocalInput(shown.endsAtUtc)
    sent.value = {
      name: shown.name,
      startsAtUtc: shown.startsAtUtc,
      endsAtUtc: shown.endsAtUtc,
    }
  },
  { immediate: true },
)

function namesTheSameMoment(left: string, right: string): boolean {
  return new Date(left).getTime() === new Date(right).getTime()
}

async function saveTheFields(): Promise<void> {
  if (festival.value === null) {
    return
  }
  const startsAtUtc = localInputToUtcIso(startsAt.value)
  const endsAtUtc = localInputToUtcIso(endsAt.value)
  const alreadyAtTheLaptop =
    name.value === sent.value.name
    && startsAtUtc !== null
    && endsAtUtc !== null
    && namesTheSameMoment(startsAtUtc, sent.value.startsAtUtc)
    && namesTheSameMoment(endsAtUtc, sent.value.endsAtUtc)
  if (alreadyAtTheLaptop) {
    return
  }
  if (name.value.trim().length === 0) {
    refusedField.value = 'name'
    return
  }
  if (startsAtUtc === null || endsAtUtc === null) {
    refusedField.value = 'period'
    return
  }
  refusedField.value = null
  sent.value = { name: name.value, startsAtUtc, endsAtUtc }
  await festivals.save(props.festivalId, { name: name.value, startsAtUtc, endsAtUtc })
}

function listenToTheLaptop(): () => void {
  const releases = [
    festivals.listen(),
    stations.listen(),
    categories.listen(),
    items.listen(),
  ]
  return () => {
    for (const release of releases) {
      release()
    }
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
}

watch(
  () => props.festivalId,
  () => {
    void reload()
  },
)

onMounted(async () => {
  stopListening = listenToTheLaptop()
  await reload()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-festival">
    <v-alert v-if="festivals.loadFailed" class="error mb-4" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>

    <template v-if="festival !== null">
      <v-card class="festival-fields mb-4" variant="outlined">
        <div class="pa-4">
          <div class="festival-heading d-flex align-center flex-wrap ga-3 mb-4">
            <h1 class="festival-name text-h5">{{ festival.name }}</h1>
            <v-chip v-if="festival.isRunning" class="running" size="small" color="success">
              {{ t('admin.festivals.running') }}
            </v-chip>
          </div>

          <div class="d-flex align-start flex-wrap ga-4">
            <v-text-field
              v-model="name"
              class="festival-name-field flex-grow-1"
              maxlength="80"
              density="compact"
              hide-details="auto"
              :error-messages="
                refusedField === 'name' ? [t('admin.festivalNameMissing')] : []
              "
              :label="t('admin.festivals.name')"
              @blur="saveTheFields"
              @keyup.enter="saveTheFields"
            />
            <v-text-field
              v-model="startsAt"
              class="festival-start-field"
              type="datetime-local"
              density="compact"
              hide-details
              :label="t('admin.festivals.start')"
              @blur="saveTheFields"
              @keyup.enter="saveTheFields"
            />
            <v-text-field
              v-model="endsAt"
              class="festival-end-field"
              type="datetime-local"
              density="compact"
              hide-details
              :label="t('admin.festivals.end')"
              @blur="saveTheFields"
              @keyup.enter="saveTheFields"
            />
          </div>
          <p v-if="refusedField === 'period'" class="period-refusal text-error text-body-2 mt-2">
            {{ t('admin.festivalPeriodInvalid') }}
          </p>
          <v-alert v-if="refusalText !== null" class="refusal mt-3" type="warning" variant="tonal">
            {{ refusalText }}
          </v-alert>
        </div>
      </v-card>

      <FestivalStations :festival-id="festivalId" />
      <FestivalItems :festival-id="festivalId" />
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
