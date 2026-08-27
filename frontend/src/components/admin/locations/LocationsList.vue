<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminLocationsStore, type AdminLocation } from '../../../stores/admin/locations'
import LocationForm from './LocationForm.vue'

const { t } = useI18n()
const locations = useAdminLocationsStore()
const editing = ref<AdminLocation | null>(null)
const isCreating = ref(false)
const stationUrl = ref<string | null>(null)

async function save(value: Parameters<typeof locations.save>[0]): Promise<void> {
  await locations.save(value)
  editing.value = null
  isCreating.value = false
}

async function regenerate(id: string): Promise<void> {
  stationUrl.value = await locations.regenerateAccessKey(id)
}

onMounted(locations.load)
</script>

<template>
  <section class="admin-locations">
    <h1>{{ t('admin.locations.title') }}</h1>
    <p class="help">{{ t('admin.locations.help') }}</p>
    <p
      v-if="locations.errorKey !== null"
      class="error"
    >
      {{ t(locations.errorKey, locations.errorParameters, Number(locations.errorParameters.count ?? 1)) }}
    </p>
    <ul>
      <li v-for="location in locations.locations" :key="location.id">
        <span class="name">{{ location.name }}</span>
        <button type="button" @click="editing = location">{{ t('admin.edit') }}</button>
        <button type="button" @click="locations.deactivate(location.id)">
          {{ t('admin.locations.deactivate') }}
        </button>
        <button type="button" @click="regenerate(location.id)">
          {{ t('admin.locations.stationCard') }}
        </button>
      </li>
    </ul>
    <p class="station-card-help">{{ t('admin.locations.stationCardHelp') }}</p>
    <p v-if="stationUrl !== null" class="station-url">{{ stationUrl }}</p>
    <button type="button" @click="isCreating = true">{{ t('admin.locations.new') }}</button>
    <LocationForm
      v-if="isCreating || editing !== null"
      :location="editing"
      @save="save"
    />
  </section>
</template>
