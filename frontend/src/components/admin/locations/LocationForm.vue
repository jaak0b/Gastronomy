<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../../core/apiTypes'
import type { AdminLocation } from '../../../stores/admin/locations'

const props = defineProps<{ location: AdminLocation | null }>()
const emit = defineEmits<{
  save: [
    value: { locationId?: string; name: string; sortOrder: number; slipLanguage: AppLanguage },
  ]
}>()

const { t } = useI18n()
const name = ref(props.location?.name ?? '')
const sortOrder = ref(props.location?.sortOrder ?? 1)
const slipLanguage = ref<AppLanguage>(props.location?.slipLanguage ?? 'de')

function save(): void {
  emit('save', {
    locationId: props.location?.locationId,
    name: name.value,
    sortOrder: sortOrder.value,
    slipLanguage: slipLanguage.value,
  })
}
</script>

<template>
  <v-card class="location-form mt-4">
    <v-form @submit.prevent="save">
      <v-card-text>
        <v-text-field v-model="name" :label="t('admin.locations.title')" />
        <v-select
          v-model="slipLanguage"
          class="slip-language"
          :label="t('admin.locations.slipLanguage')"
          :items="[
            { title: t('settings.languageGerman'), value: 'de' },
            { title: t('settings.languageEnglish'), value: 'en' },
          ]"
        />
        <p class="help text-medium-emphasis mt-2">{{ t('admin.locations.slipLanguageHelp') }}</p>
      </v-card-text>
      <v-card-actions>
        <v-btn type="submit" color="primary" :disabled="name.trim().length === 0">
          {{ t('admin.save') }}
        </v-btn>
      </v-card-actions>
    </v-form>
  </v-card>
</template>
