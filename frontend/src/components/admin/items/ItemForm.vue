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
const emit = defineEmits<{
  save: [item: AdminItemDraft]
  setActive: [id: string, isActive: boolean]
}>()

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
  <form class="item-form" @submit.prevent="save">
    <label>
      <span>{{ t('admin.items.title') }}</span>
      <input v-model="name" type="text" />
    </label>
    <label>
      <span>{{ t('admin.items.category') }}</span>
      <input v-model="categoryName" type="text" />
    </label>
    <label class="price-field">
      <span>{{ t('admin.items.price') }}</span>
      <input v-model="priceText" type="text" inputmode="decimal" />
    </label>
    <p v-if="priceIsUnreadable" class="price-error error">{{ t('admin.items.priceInvalid') }}</p>
    <p class="help">{{ t('admin.items.priceHelp') }}</p>
    <AssignmentEditor
      :item-name="name"
      :locations="locations"
      :selected-location-ids="locationIds"
      @toggle="toggle"
    />
    <p v-if="errorKey !== null" class="error">{{ t(errorKey) }}</p>
    <button type="submit" :disabled="name.trim().length === 0">{{ t('admin.save') }}</button>
    <template v-if="item !== null">
      <button
        type="button"
        class="toggle-active"
        @click="emit('setActive', item.itemId, !item.isActive)"
      >
        {{ item.isActive ? t('admin.items.deactivate') : t('admin.items.activate') }}
      </button>
      <p class="help">{{ t('admin.items.deactivateHelp') }}</p>
    </template>
  </form>
</template>
