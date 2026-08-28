<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../../core/apiTypes'
import { formatEuroInput, parseEuroInput } from '../../../core/money'
import type { AdminItem, AdminItemDraft } from '../../../stores/admin/items'
import type { AdminStation } from '../../../stores/admin/stations'
import AssignmentEditor from './AssignmentEditor.vue'

const props = defineProps<{
  item: AdminItem | null
  stations: AdminStation[]
  categoryNames: string[]
  errorKey: string | null
  isCancellable?: boolean
}>()
const emit = defineEmits<{ save: [item: AdminItemDraft]; cancel: [] }>()

const { t, locale } = useI18n()
const name = ref(props.item?.name ?? '')
const categoryName = ref(props.item?.categoryName ?? '')
const priceText = ref(
  formatEuroInput(props.item?.priceCents ?? null, locale.value as AppLanguage),
)
const priceIsUnreadable = ref(false)
const sortOrder = ref(props.item?.sortOrder ?? 1)
const stationIds = ref<string[]>([...(props.item?.stationIds ?? [])])

function toggle(stationId: string): void {
  stationIds.value = stationIds.value.includes(stationId)
    ? stationIds.value.filter((id) => id !== stationId)
    : [...stationIds.value, stationId]
}

function save(): void {
  const priceCents = parseEuroInput(priceText.value)
  priceIsUnreadable.value = priceCents === null
  if (priceCents === null) {
    return
  }
  emit('save', {
    itemId: props.item?.itemId,
    name: name.value,
    categoryName: categoryName.value,
    priceCents,
    sortOrder: sortOrder.value,
    stationIds: stationIds.value,
  })
}
</script>

<template>
  <v-card class="item-form mt-4">
    <v-form @submit.prevent="save">
      <v-card-text>
        <v-text-field v-model="name" class="mb-4" :label="t('admin.items.title')" />
        <v-combobox
          v-model="categoryName"
          class="category-field mb-4"
          :label="t('admin.items.category')"
          :items="categoryNames"
        />
        <v-text-field
          v-model="priceText"
          class="price-field mb-2"
          :label="t('admin.items.price')"
          inputmode="decimal"
          :error="priceIsUnreadable"
          :error-messages="priceIsUnreadable ? [t('admin.items.priceInvalid')] : []"
        />
        <AssignmentEditor
          :item-name="name"
          :stations="stations"
          :selected-station-ids="stationIds"
          @toggle="toggle"
        />
        <v-alert v-if="errorKey !== null" class="error" type="error" variant="tonal">
          {{ t(errorKey) }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-btn type="submit" color="primary" :disabled="name.trim().length === 0">
          {{ t('admin.save') }}
        </v-btn>
        <v-btn v-if="props.isCancellable" class="cancel" variant="text" @click="emit('cancel')">
          {{ t('admin.cancel') }}
        </v-btn>
      </v-card-actions>
    </v-form>
  </v-card>
</template>
