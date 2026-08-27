<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminItemsStore, type AdminItemDraft } from '../../../stores/admin/items'
import { useAdminLocationsStore } from '../../../stores/admin/locations'
import ConfirmDialog from '../ConfirmDialog.vue'
import TrashIcon from '../TrashIcon.vue'
import ItemForm from './ItemForm.vue'

const { t } = useI18n()
const items = useAdminItemsStore()
const locations = useAdminLocationsStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)

const shown = computed(() =>
  items.items.filter((item) => showsDeactivated.value || item.isActive),
)

const editing = computed(() => items.items.find((item) => item.itemId === editingId.value) ?? null)

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
  await locations.load()
})
</script>

<template>
  <section class="admin-items">
    <h1>{{ t('admin.items.title') }}</h1>
    <p class="help">{{ t('admin.items.soldOutHelp') }}</p>
    <p class="walk">{{ t('admin.items.soldOutWalk') }}</p>
    <p v-if="items.errorKey !== null" class="error">{{ t(items.errorKey) }}</p>
    <p v-if="items.loadFailed" class="error">{{ t('admin.loadFailed') }}</p>
    <p v-else-if="items.items.length === 0" class="empty">{{ t('admin.items.empty') }}</p>
    <label class="show-deactivated-field">
      <input v-model="showsDeactivated" type="checkbox" class="show-deactivated" />
      <span>{{ t('admin.showDeactivated') }}</span>
    </label>
    <ul>
      <li v-for="item in shown" :key="item.itemId">
        <span class="name">{{ item.name }}</span>
        <span v-if="!item.isActive" class="deactivated">{{ t('admin.deactivated') }}</span>
        <button
          v-if="item.isActive"
          type="button"
          class="sold-out-toggle"
          @click="items.setAvailability(item.itemId, !item.isAvailable)"
        >
          {{ item.isAvailable ? t('admin.items.soldOut') : t('admin.items.soldOutUndo') }}
        </button>
        <button type="button" @click="editingId = item.itemId">{{ t('admin.edit') }}</button>
        <button
          v-if="item.isActive"
          type="button"
          class="deactivate"
          :aria-label="t('admin.deactivate')"
          :title="t('admin.deactivate')"
          @click="askingAboutId = item.itemId"
        >
          <TrashIcon />
        </button>
        <button
          v-else
          type="button"
          class="reactivate"
          @click="items.setActive(item.itemId, true)"
        >
          {{ t('admin.items.activate') }}
        </button>
      </li>
    </ul>
    <button type="button" @click="isCreating = true">{{ t('admin.items.new') }}</button>
    <ItemForm
      v-if="isCreating || editing !== null"
      :item="editing"
      :locations="locations.locations"
      :error-key="items.errorKey"
      @save="save"
    />
    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.items.deactivateTitle')"
      :body="t('admin.items.deactivateBody')"
      :confirm-label="t('admin.items.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
  </section>
</template>
