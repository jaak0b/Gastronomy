<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { groupByCategory } from '../../../core/grouping'
import { useAdminItemsStore, type AdminItemDraft } from '../../../stores/admin/items'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import ConfirmDialog from '../ConfirmDialog.vue'
import ItemForm from './ItemForm.vue'
import NewItemDialog from './NewItemDialog.vue'

const { t } = useI18n()
const items = useAdminItemsStore()
const stations = useAdminStationsStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)

const groups = computed(() =>
  groupByCategory(
    items.items.filter((item) => showsDeactivated.value || item.isActive),
    (item) => item.categoryName,
    (item) => item.name,
  ),
)

const refusal = computed(() => {
  const message = items.errorMessage
  if (message === null) {
    return null
  }
  return message.count === null
    ? t(message.key, message.parameters)
    : t(message.key, message.parameters, message.count)
})

async function save(item: AdminItemDraft): Promise<void> {
  const saved = await items.save(item)
  if (saved) {
    editingId.value = null
    isCreating.value = false
  }
}

function toggleEditing(itemId: string): void {
  editingId.value = editingId.value === itemId ? null : itemId
  items.forgetError()
}

function startCreating(): void {
  isCreating.value = true
  items.forgetError()
}

function stopCreating(): void {
  isCreating.value = false
  items.forgetError()
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
    <v-alert
      v-if="refusal !== null && editingId === null && !isCreating"
      class="error mb-4"
      type="error"
      variant="tonal"
    >
      {{ refusal }}
    </v-alert>
    <v-alert v-if="items.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>

    <v-checkbox
      v-model="showsDeactivated"
      class="show-deactivated"
      :label="t('admin.showDeactivated')"
    />

    <section v-for="group in groups" :key="group.name" class="category-section">
      <h2 class="category-heading text-subtitle-1 font-weight-bold py-2">{{ group.name }}</h2>
      <v-card v-for="item in group.items" :key="item.itemId" class="item-row mb-2">
        <div class="item-line d-flex align-center ga-2 px-4 py-2">
          <span class="name text-body-1">{{ item.name }}</span>
          <v-chip v-if="!item.isActive" class="deactivated" size="small" color="grey">
            {{ t('admin.deactivated') }}
          </v-chip>
          <v-spacer />
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
            @click="toggleEditing(item.itemId)"
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
        </div>
        <v-expand-transition>
          <ItemForm
            v-if="editingId === item.itemId"
            :item="item"
            :stations="stations.stations"
            :category-names="items.categoryNames"
            :error-text="refusal"
            @save="save"
          />
        </v-expand-transition>
      </v-card>
    </section>

    <v-btn class="new-item" color="primary" @click="startCreating">
      {{ t('admin.items.new') }}
    </v-btn>

    <NewItemDialog
      v-if="isCreating"
      :stations="stations.stations"
      :category-names="items.categoryNames"
      :error-text="refusal"
      @save="save"
      @cancel="stopCreating"
    />
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
