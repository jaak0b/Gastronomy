<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../../core/apiTypes'
import type { AdminLocation } from '../../../stores/admin/locations'

const props = defineProps<{ location: AdminLocation | null }>()
const emit = defineEmits<{
  save: [value: { id?: string; name: string; sortOrder: number; slipLanguage: AppLanguage }]
}>()

const { t } = useI18n()
const name = ref(props.location?.name ?? '')
const sortOrder = ref(props.location?.sortOrder ?? 1)
const slipLanguage = ref<AppLanguage>(props.location?.slipLanguage ?? 'de')

function save(): void {
  emit('save', {
    id: props.location?.id,
    name: name.value,
    sortOrder: sortOrder.value,
    slipLanguage: slipLanguage.value,
  })
}
</script>

<template>
  <form class="location-form" @submit.prevent="save">
    <label>
      <span>{{ t('admin.locations.title') }}</span>
      <input v-model="name" type="text" />
    </label>
    <label>
      <span>{{ t('admin.locations.slipLanguage') }}</span>
      <select v-model="slipLanguage">
        <option value="de">{{ t('settings.languageGerman') }}</option>
        <option value="en">{{ t('settings.languageEnglish') }}</option>
      </select>
    </label>
    <p class="help">{{ t('admin.locations.slipLanguageHelp') }}</p>
    <button type="submit" :disabled="name.trim().length === 0">{{ t('admin.save') }}</button>
  </form>
</template>
