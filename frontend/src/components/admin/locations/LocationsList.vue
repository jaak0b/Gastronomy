<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminLocationsStore } from '../../../stores/admin/locations'
import ConfirmDialog from '../ConfirmDialog.vue'
import LocationForm from './LocationForm.vue'

const { t } = useI18n()
const locations = useAdminLocationsStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)

const shown = computed(() =>
  locations.locations.filter((location) => showsDeactivated.value || location.isActive),
)

const editing = computed(
  () => locations.locations.find((location) => location.locationId === editingId.value) ?? null,
)

const refusal = computed(() => {
  const message = locations.errorMessage
  if (message === null) {
    return null
  }
  return message.count === null
    ? t(message.key, message.parameters)
    : t(message.key, message.parameters, message.count)
})

async function save(value: Parameters<typeof locations.save>[0]): Promise<void> {
  await locations.save(value)
  editingId.value = null
  isCreating.value = false
}

async function deactivate(): Promise<void> {
  const locationId = askingAboutId.value
  askingAboutId.value = null
  if (locationId !== null) {
    await locations.setActive(locationId, false)
  }
}

onMounted(locations.load)
</script>

<template>
  <v-container class="admin-locations">
    <h1 class="text-h5 mb-2">{{ t('admin.locations.title') }}</h1>
    <p class="help text-medium-emphasis mb-4">{{ t('admin.locations.help') }}</p>

    <v-alert v-if="refusal !== null" class="refusal mb-4" type="warning" variant="tonal">
      {{ refusal }}
    </v-alert>
    <v-alert v-if="locations.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>
    <v-alert v-else-if="locations.locations.length === 0" class="empty" type="info" variant="tonal">
      {{ t('admin.locations.empty') }}
    </v-alert>

    <v-checkbox
      v-model="showsDeactivated"
      class="show-deactivated"
      :label="t('admin.showDeactivated')"
    />

    <v-card v-for="location in shown" :key="location.locationId" class="station-row mb-3">
      <v-card-item>
        <v-card-title class="name">
          {{ location.name }}
          <v-chip v-if="!location.isActive" class="deactivated ms-2" size="small" color="grey">
            {{ t('admin.deactivated') }}
          </v-chip>
        </v-card-title>
      </v-card-item>
      <v-card-actions>
        <v-btn class="edit" variant="text" @click="editingId = location.locationId">
          {{ t('admin.edit') }}
        </v-btn>
        <v-btn
          v-if="location.isActive"
          class="deactivate"
          icon="mdi-delete"
          variant="text"
          color="error"
          :aria-label="t('admin.deactivate')"
          @click="askingAboutId = location.locationId"
        />
        <v-btn
          v-else
          class="reactivate"
          variant="text"
          @click="locations.setActive(location.locationId, true)"
        >
          {{ t('admin.locations.activate') }}
        </v-btn>
      </v-card-actions>
    </v-card>

    <v-btn class="new-station" color="primary" @click="isCreating = true">
      {{ t('admin.locations.new') }}
    </v-btn>

    <LocationForm v-if="isCreating || editing !== null" :location="editing" @save="save" />

    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.locations.deactivateTitle')"
      :body="t('admin.locations.deactivateBody')"
      :confirm-label="t('admin.locations.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
  </v-container>
</template>
