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

async function deactivate(id: string): Promise<void> {
  await items.deactivate(id)
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
    <ul>
      <li v-for="item in items.items" :key="item.id">
        <span class="name">{{ item.name }}</span>
        <button
          type="button"
          class="sold-out-toggle"
          @click="items.setAvailability(item.id, !item.isAvailable)"
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
      @deactivate="deactivate"
    />
  </section>
</template>
