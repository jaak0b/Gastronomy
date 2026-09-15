<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminErrorMessage } from '../../../core/adminErrorMessage'
import type { AppLanguage } from '../../../core/apiTypes'
import { assertNever } from '../../../core/assertNever'
import { formatEuroInput, parseEuroInput } from '../../../core/money'
import { useAdminItemsStore, type AdminItem } from '../../../stores/admin/items'
import type { AdminStation } from '../../../stores/admin/stations'
import { useRefusalText } from '../refusalText'
import StationSelect from './StationSelect.vue'

const props = defineProps<{
  festivalId: string
  item: AdminItem
  stations: AdminStation[]
}>()
const emit = defineEmits<{ placed: []; cancel: [] }>()

const { t, locale } = useI18n()
const items = useAdminItemsStore()
const priceText = ref(
  formatEuroInput(props.item.atTheFestival?.priceCents ?? null, locale.value as AppLanguage),
)
const selectedStationIds = ref<string[]>([...(props.item.atTheFestival?.stationIds ?? [])])
const priceRefused = ref(false)
const stationRefused = ref(false)
const isSending = ref(false)
const refusal = ref<AdminErrorMessage | null>(null)

const refusalText = useRefusalText(refusal)

const stationErrorText = computed(() =>
  stationRefused.value ? t('admin.festival.itemNeedsAStation', { item: props.item.name }) : null,
)

function typePrice(typed: string): void {
  priceText.value = typed
  priceRefused.value = false
}

function chooseStations(stationIds: string[]): void {
  selectedStationIds.value = stationIds
  stationRefused.value = false
}

async function place(): Promise<void> {
  const priceCents = parseEuroInput(priceText.value)
  priceRefused.value = priceCents === null
  stationRefused.value = selectedStationIds.value.length === 0
  if (priceCents === null || stationRefused.value) {
    return
  }
  isSending.value = true
  refusal.value = null
  const placed = await items.putAtTheFestival(props.festivalId, props.item.itemId, {
    priceCents,
    stationIds: [...selectedStationIds.value],
  })
  isSending.value = false
  switch (placed.kind) {
    case 'ok':
      emit('placed')
      return
    case 'failed':
      refusal.value = placed.message
      return
    default:
      assertNever(placed)
  }
}
</script>

<template>
  <v-dialog :model-value="true" max-width="560" persistent scrollable>
    <v-card class="placement-dialog" role="dialog" aria-modal="true">
      <v-card-title class="placement-title">
        {{ t('admin.festival.addItem') }}: {{ item.name }}
      </v-card-title>
      <v-card-text>
        <v-text-field
          class="price-field"
          density="compact"
          inputmode="decimal"
          hide-details="auto"
          :model-value="priceText"
          :label="t('admin.items.price')"
          :error="priceRefused"
          :error-messages="priceRefused ? [t('admin.items.priceInvalid')] : []"
          @update:model-value="(typed: string) => typePrice(typed)"
        />
        <StationSelect
          class="placement-stations"
          :stations="stations"
          :selected-station-ids="selectedStationIds"
          :error-text="stationErrorText"
          @select="chooseStations"
        />
        <v-alert v-if="refusalText !== null" class="refusal mt-3" type="warning" variant="tonal">
          {{ refusalText }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-btn class="cancel" variant="text" :disabled="isSending" @click="emit('cancel')">
          {{ t('admin.cancel') }}
        </v-btn>
        <v-btn class="place-item" color="primary" :disabled="isSending" @click="place">
          {{ t('admin.festival.addItem') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.placement-stations {
  margin-top: 16px;
}
</style>
