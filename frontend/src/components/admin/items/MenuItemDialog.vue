<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../../core/apiTypes'
import { formatEuroInput, parseEuroInput } from '../../../core/money'
import type { MenuPlacement } from '../../../stores/admin/items'
import type { AdminStation } from '../../../stores/admin/stations'
import StationChips from './StationChips.vue'

const props = defineProps<{
  itemName: string
  stations: AdminStation[]
  priceCents: number | null
  stationIds: string[]
  errorText: string | null
}>()
const emit = defineEmits<{ confirm: [placement: MenuPlacement]; cancel: [] }>()

const { t, locale } = useI18n()
const priceText = ref(formatEuroInput(props.priceCents, locale.value as AppLanguage))
const priceIsUnreadable = ref(false)
const chosenStationIds = ref<string[]>([...props.stationIds])
const noStationIsChosen = ref(false)

function toggle(stationId: string): void {
  noStationIsChosen.value = false
  chosenStationIds.value = chosenStationIds.value.includes(stationId)
    ? chosenStationIds.value.filter((id) => id !== stationId)
    : [...chosenStationIds.value, stationId]
}

function confirm(): void {
  const priceCents = parseEuroInput(priceText.value)
  priceIsUnreadable.value = priceCents === null
  noStationIsChosen.value = chosenStationIds.value.length === 0
  if (priceCents === null || chosenStationIds.value.length === 0) {
    return
  }
  emit('confirm', { priceCents, stationIds: [...chosenStationIds.value] })
}
</script>

<template>
  <v-dialog :model-value="true" max-width="560" persistent scrollable>
    <v-card class="menu-item-dialog" role="dialog" aria-modal="true">
      <v-card-title class="menu-item-title">{{ t('admin.items.putOnTheMenuTitle') }}</v-card-title>
      <v-card-text>
        <p class="item-name text-body-1 mb-4">{{ itemName }}</p>
        <v-text-field
          v-model="priceText"
          class="price-field mb-2"
          :label="t('admin.items.price')"
          inputmode="decimal"
          :error="priceIsUnreadable"
          :error-messages="priceIsUnreadable ? [t('admin.items.priceInvalid')] : []"
        />
        <StationChips
          :stations="stations"
          :selected-station-ids="chosenStationIds"
          @toggle="toggle"
        />
        <v-alert
          v-if="noStationIsChosen"
          class="needs-a-station mt-4"
          type="warning"
          variant="tonal"
        >
          {{ t('admin.itemNeedsAStation') }}
        </v-alert>
        <v-alert v-if="errorText !== null" class="error mt-4" type="error" variant="tonal">
          {{ errorText }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn class="cancel" variant="text" @click="emit('cancel')">
          {{ t('admin.cancel') }}
        </v-btn>
        <v-btn class="confirm" color="primary" @click="confirm">
          {{ t('admin.items.putOnTheMenu') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
