<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminLocationsStore, type AdminLocation } from '../../../stores/admin/locations'
import LocationForm from './LocationForm.vue'

const { t } = useI18n()
const locations = useAdminLocationsStore()
const editing = ref<AdminLocation | null>(null)
const isCreating = ref(false)

async function save(value: Parameters<typeof locations.save>[0]): Promise<void> {
  await locations.save(value)
  editing.value = null
  isCreating.value = false
}

onMounted(locations.load)
</script>

<template>
  <section class="admin-locations">
    <h1>{{ t('admin.locations.title') }}</h1>
    <p class="help">{{ t('admin.locations.help') }}</p>
    <p v-if="locations.errorMessage !== null" class="refusal error">
      {{
        locations.errorMessage.count === null
          ? t(locations.errorMessage.key, locations.errorMessage.parameters)
          : t(
              locations.errorMessage.key,
              locations.errorMessage.parameters,
              locations.errorMessage.count,
            )
      }}
    </p>
    <p v-if="locations.loadFailed" class="error">{{ t('admin.loadFailed') }}</p>
    <p v-else-if="locations.locations.length === 0" class="empty">
      {{ t('admin.locations.empty') }}
    </p>
    <ul>
      <li v-for="location in locations.locations" :key="location.locationId">
        <span class="name">{{ location.name }}</span>
        <span v-if="!location.isActive" class="switched-off">
          {{ t('admin.locations.switchedOff') }}
        </span>
        <button type="button" @click="editing = location">{{ t('admin.edit') }}</button>
        <button
          type="button"
          class="toggle-active"
          @click="locations.setActive(location.locationId, !location.isActive)"
        >
          {{
            location.isActive
              ? t('admin.locations.deactivate')
              : t('admin.locations.activate')
          }}
        </button>
      </li>
    </ul>
    <button type="button" @click="isCreating = true">{{ t('admin.locations.new') }}</button>
    <LocationForm
      v-if="isCreating || editing !== null"
      :location="editing"
      @save="save"
    />
  </section>
</template>
