<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminErrorMessage } from '../../core/adminErrorMessage'
import { AdminItemView, AdminStationView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { appLanguageOf } from '../../../shared/core/deviceLanguage'
import { formatEuroInput, parseEuroInput } from '../../../shared/core/money'
import { useAdminItemsStore } from '../../stores/items'
import BaseFormDialog from '../BaseFormDialog.vue'
import { useRefusalText } from '../../composables/useRefusalText'
import StationSelect from './StationSelect.vue'

const props = defineProps<{
  festivalId: string
  item: AdminItemView
  stations: AdminStationView[]
}>()
const emit = defineEmits<{ placed: []; cancel: [] }>()

const { t, locale } = useI18n()
const items = useAdminItemsStore()
const priceText = ref(
  formatEuroInput(props.item.atTheFestival?.priceCents ?? null, appLanguageOf(locale.value)),
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
  <BaseFormDialog
    :title="`${t('admin.festival.addItem')}: ${item.name}`"
    :error-text="refusalText"
    :save-label="t('admin.festival.addItem')"
    :busy="isSending"
    @save="place"
    @cancel="emit('cancel')"
  >
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
  </BaseFormDialog>
</template>

<style scoped>
.placement-stations {
  margin-top: 16px;
}
</style>
