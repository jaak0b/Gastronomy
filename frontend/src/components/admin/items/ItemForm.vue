<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../../core/apiTypes'
import { formatEuroInput, parseEuroInput } from '../../../core/money'
import type { AdminItem, AdminItemDraft } from '../../../stores/admin/items'
import type { AdminLocation } from '../../../stores/admin/locations'
import AssignmentEditor from './AssignmentEditor.vue'

const props = defineProps<{
  item: AdminItem | null
  locations: AdminLocation[]
  errorKey: string | null
}>()
const emit = defineEmits<{ save: [item: AdminItemDraft] }>()

const { t, locale } = useI18n()
const name = ref(props.item?.name ?? '')
const categoryName = ref(props.item?.categoryName ?? '')
const priceText = ref(
  formatEuroInput(props.item?.priceCents ?? null, locale.value as AppLanguage),
)
const priceIsUnreadable = ref(false)
const sortOrder = ref(props.item?.sortOrder ?? 1)
const locationIds = ref<string[]>([...(props.item?.locationIds ?? [])])

function toggle(locationId: string): void {
  locationIds.value = locationIds.value.includes(locationId)
    ? locationIds.value.filter((id) => id !== locationId)
    : [...locationIds.value, locationId]
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
    locationIds: locationIds.value,
  })
}
</script>

<template>
  <v-card class="item-form mt-4">
    <v-form @submit.prevent="save">
      <v-card-text>
        <v-text-field v-model="name" class="mb-4" :label="t('admin.items.title')" />
        <v-text-field v-model="categoryName" class="mb-4" :label="t('admin.items.category')" />
        <v-text-field
          v-model="priceText"
          class="price-field mb-2"
          :label="t('admin.items.price')"
          inputmode="decimal"
          :error="priceIsUnreadable"
          :error-messages="priceIsUnreadable ? [t('admin.items.priceInvalid')] : []"
        />
        <p class="help text-medium-emphasis">{{ t('admin.items.priceHelp') }}</p>
        <AssignmentEditor
          :item-name="name"
          :locations="locations"
          :selected-location-ids="locationIds"
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
      </v-card-actions>
    </v-form>
  </v-card>
</template>
