<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminItemsStore, type AdminItem, type AdminItemDraft } from '../../../stores/admin/items'
import { useAdminLocationsStore } from '../../../stores/admin/locations'
import ItemForm from './ItemForm.vue'

const { t } = useI18n()
const items = useAdminItemsStore()
const locations = useAdminLocationsStore()
const editing = ref<AdminItem | null>(null)
const isCreating = ref(false)

async function save(item: AdminItemDraft): Promise<void> {
  const saved = await items.save(item)
  if (saved) {
    editing.value = null
    isCreating.value = false
  }
}

async function setActive(id: string, isActive: boolean): Promise<void> {
  await items.setActive(id, isActive)
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
    <ul>
      <li v-for="item in items.items" :key="item.itemId">
        <span class="name">{{ item.name }}</span>
        <span v-if="!item.isActive" class="off-the-menu">{{ t('admin.items.offTheMenu') }}</span>
        <button
          type="button"
          class="sold-out-toggle"
          @click="items.setAvailability(item.itemId, !item.isAvailable)"
        >
          {{ item.isAvailable ? t('admin.items.soldOut') : t('admin.items.soldOutUndo') }}
        </button>
        <button type="button" @click="editing = item">{{ t('admin.edit') }}</button>
      </li>
    </ul>
    <button type="button" @click="isCreating = true">{{ t('admin.items.new') }}</button>
    <ItemForm
      v-if="isCreating || editing !== null"
      :item="editing"
      :locations="locations.locations"
      :error-key="items.errorKey"
      @save="save"
      @set-active="setActive"
    />
  </section>
</template>
