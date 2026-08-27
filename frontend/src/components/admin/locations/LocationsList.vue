<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminLocationsStore } from '../../../stores/admin/locations'
import ConfirmDialog from '../ConfirmDialog.vue'
import TrashIcon from '../TrashIcon.vue'
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
    <label class="show-deactivated-field">
      <input v-model="showsDeactivated" type="checkbox" class="show-deactivated" />
      <span>{{ t('admin.showDeactivated') }}</span>
    </label>
    <ul>
      <li v-for="location in shown" :key="location.locationId">
        <span class="name">{{ location.name }}</span>
        <span v-if="!location.isActive" class="deactivated">{{ t('admin.deactivated') }}</span>
        <button type="button" @click="editingId = location.locationId">
          {{ t('admin.edit') }}
        </button>
        <button
          v-if="location.isActive"
          type="button"
          class="deactivate"
          :aria-label="t('admin.deactivate')"
          :title="t('admin.deactivate')"
          @click="askingAboutId = location.locationId"
        >
          <TrashIcon />
        </button>
        <button
          v-else
          type="button"
          class="reactivate"
          @click="locations.setActive(location.locationId, true)"
        >
          {{ t('admin.locations.activate') }}
        </button>
      </li>
    </ul>
    <button type="button" @click="isCreating = true">{{ t('admin.locations.new') }}</button>
    <LocationForm v-if="isCreating || editing !== null" :location="editing" @save="save" />
    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.locations.deactivateTitle')"
      :body="t('admin.locations.deactivateBody')"
      :confirm-label="t('admin.locations.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
  </section>
</template>
