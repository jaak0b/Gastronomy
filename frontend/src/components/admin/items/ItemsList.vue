<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminItemsStore, type AdminItemDraft } from '../../../stores/admin/items'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import ConfirmDialog from '../ConfirmDialog.vue'
import ItemForm from './ItemForm.vue'

const { t } = useI18n()
const items = useAdminItemsStore()
const stations = useAdminStationsStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)

const shown = computed(() =>
  items.items.filter((item) => showsDeactivated.value || item.isActive),
)


async function save(item: AdminItemDraft): Promise<void> {
  const saved = await items.save(item)
  if (saved) {
    editingId.value = null
    isCreating.value = false
  }
}

async function deactivate(): Promise<void> {
  const itemId = askingAboutId.value
  askingAboutId.value = null
  if (itemId !== null) {
    await items.setActive(itemId, false)
  }
}

onMounted(async () => {
  await items.load()
  await stations.load()
})
</script>

<template>
  <v-container class="admin-items">
    <h1 class="text-h5 mb-2">{{ t('admin.items.title') }}</h1>
    <p class="help text-medium-emphasis">{{ t('admin.items.soldOutHelp') }}</p>
    <p class="walk text-medium-emphasis mb-4">{{ t('admin.items.soldOutWalk') }}</p>

    <v-alert v-if="items.errorKey !== null" class="error mb-4" type="error" variant="tonal">
      {{ t(items.errorKey) }}
    </v-alert>
    <v-alert v-if="items.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>
    <v-alert v-else-if="items.items.length === 0" class="empty" type="info" variant="tonal">
      {{ t('admin.items.empty') }}
    </v-alert>

    <v-checkbox
      v-model="showsDeactivated"
      class="show-deactivated"
      :label="t('admin.showDeactivated')"
    />

    <v-card v-for="item in shown" :key="item.itemId" class="item-row mb-3">
      <v-card-item>
        <v-card-title class="name">
          {{ item.name }}
          <v-chip v-if="!item.isActive" class="deactivated ms-2" size="small" color="grey">
            {{ t('admin.deactivated') }}
          </v-chip>
        </v-card-title>
      </v-card-item>
      <v-card-actions>
        <v-btn
          v-if="item.isActive"
          class="sold-out-toggle"
          variant="text"
          @click="items.setAvailability(item.itemId, !item.isAvailable)"
        >
          {{ item.isAvailable ? t('admin.items.soldOut') : t('admin.items.soldOutUndo') }}
        </v-btn>
        <v-btn
          class="edit"
          variant="text"
          @click="editingId = editingId === item.itemId ? null : item.itemId"
        >
          {{ t('admin.edit') }}
        </v-btn>
        <v-btn
          v-if="item.isActive"
          class="deactivate"
          icon="mdi-delete"
          variant="text"
          color="error"
          :aria-label="t('admin.deactivate')"
          @click="askingAboutId = item.itemId"
        />
        <v-btn
          v-else
          class="reactivate"
          variant="text"
          @click="items.setActive(item.itemId, true)"
        >
          {{ t('admin.items.activate') }}
        </v-btn>
      </v-card-actions>
      <v-expand-transition>
        <ItemForm
          v-if="editingId === item.itemId"
          :item="item"
          :stations="stations.stations"
          :error-key="items.errorKey"
          @save="save"
        />
      </v-expand-transition>
    </v-card>

    <v-btn class="new-item" color="primary" @click="isCreating = true">
      {{ t('admin.items.new') }}
    </v-btn>

    <v-card v-if="isCreating" class="item-row mt-3">
      <ItemForm
        :item="null"
        :stations="stations.stations"
        :error-key="items.errorKey"
        @save="save"
      />
    </v-card>
    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.items.deactivateTitle')"
      :body="t('admin.items.deactivateBody')"
      :confirm-label="t('admin.items.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
  </v-container>
</template>
