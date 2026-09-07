<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminCategory } from '../../../core/apiTypes'
import { groupByCategory } from '../../../core/grouping'
import { letteringColourOn } from '../../../core/letteringColour'
import {
  useAdminCategoriesStore,
  type AdminCategoryDraft,
} from '../../../stores/admin/categories'
import { useAdminItemsStore, type AdminItemDraft } from '../../../stores/admin/items'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import CategoryDialog from '../categories/CategoryDialog.vue'
import ConfirmDialog from '../ConfirmDialog.vue'
import { useRefusalText } from '../refusalText'
import ItemForm from './ItemForm.vue'
import NewItemDialog from './NewItemDialog.vue'

const { t } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const stations = useAdminStationsStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const askingAboutCategoryId = ref<string | null>(null)
const renamedCategory = ref<AdminCategory | null>(null)
const isCreatingCategory = ref(false)
let stopListening: (() => void) | null = null

const groups = computed(() =>
  groupByCategory(
    categories.categories,
    items.items.filter((item) => showsDeactivated.value || item.isActive),
    (category) => category.categoryId,
    (item) => item.categoryId,
    (item) => item.name,
  ),
)

const itemRefusal = useRefusalText([() => items.errorMessage])
const categoryRefusal = useRefusalText([() => categories.errorMessage])
const refusal = computed(() => itemRefusal.value ?? categoryRefusal.value)
const isCategoryDialogOpen = computed(
  () => isCreatingCategory.value || renamedCategory.value !== null,
)

async function save(item: AdminItemDraft): Promise<void> {
  const saved = await items.save(item)
  if (saved) {
    editingId.value = null
    isCreating.value = false
  }
}

function forgetRefusals(): void {
  items.forgetError()
  categories.forgetError()
}

function toggleEditing(itemId: string): void {
  editingId.value = editingId.value === itemId ? null : itemId
  forgetRefusals()
}

function startCreating(): void {
  isCreating.value = true
  forgetRefusals()
}

function stopCreating(): void {
  isCreating.value = false
  forgetRefusals()
}

async function deactivate(): Promise<void> {
  const itemId = askingAboutId.value
  askingAboutId.value = null
  if (itemId !== null) {
    await items.setActive(itemId, false)
  }
}

function startCreatingCategory(): void {
  forgetRefusals()
  renamedCategory.value = null
  isCreatingCategory.value = true
}

function startRenamingCategory(category: AdminCategory): void {
  forgetRefusals()
  isCreatingCategory.value = false
  renamedCategory.value = category
}

function stopEditingCategory(): void {
  forgetRefusals()
  isCreatingCategory.value = false
  renamedCategory.value = null
}

async function saveCategory(draft: AdminCategoryDraft): Promise<void> {
  const category = renamedCategory.value
  const saved =
    category === null
      ? (await categories.create(draft)) !== null
      : await categories.save({ categoryId: category.categoryId, ...draft })
  if (saved) {
    stopEditingCategory()
  }
}

async function deactivateCategory(): Promise<void> {
  const categoryId = askingAboutCategoryId.value
  askingAboutCategoryId.value = null
  if (categoryId !== null) {
    await categories.setActive(categoryId, false)
  }
}

onMounted(async () => {
  stopListening = categories.listen()
  await categories.load()
  await items.load()
  await stations.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-items">
    <v-alert
      v-if="refusal !== null && editingId === null && !isCreating && !isCategoryDialogOpen"
      class="refusal mb-4"
      type="warning"
      variant="tonal"
    >
      {{ refusal }}
    </v-alert>
    <v-alert
      v-if="items.loadFailed || categories.loadFailed"
      class="error"
      type="error"
      variant="tonal"
    >
      {{ t('admin.loadFailed') }}
    </v-alert>

    <v-checkbox
      v-model="showsDeactivated"
      class="show-deactivated"
      :label="t('admin.showDeactivated')"
    />

    <section
      v-for="group in groups"
      :key="group.category.categoryId"
      class="category-section"
    >
      <div class="category-heading d-flex align-center ga-2 py-2">
        <h2
          class="category-name text-subtitle-1 font-weight-bold px-3 py-1 rounded"
          :style="{
            backgroundColor: group.category.colourHex,
            color: letteringColourOn(group.category.colourHex),
          }"
        >
          {{ group.category.name }}
        </h2>
        <v-chip v-if="!group.category.isActive" class="deactivated" size="small" color="grey">
          {{ t('admin.deactivated') }}
        </v-chip>
        <v-spacer />
        <v-btn
          class="rename-category"
          icon="mdi-pencil"
          variant="text"
          :aria-label="t('admin.categories.edit')"
          @click="startRenamingCategory(group.category)"
        />
        <v-btn
          class="move-category-up"
          icon="mdi-arrow-up"
          variant="text"
          :aria-label="t('admin.categories.moveUp')"
          @click="categories.move(group.category.categoryId, 'up')"
        />
        <v-btn
          class="move-category-down"
          icon="mdi-arrow-down"
          variant="text"
          :aria-label="t('admin.categories.moveDown')"
          @click="categories.move(group.category.categoryId, 'down')"
        />
        <v-btn
          v-if="group.category.isActive"
          class="deactivate-category"
          icon="mdi-eye-off"
          variant="text"
          color="error"
          :aria-label="t('admin.categories.deactivate')"
          @click="askingAboutCategoryId = group.category.categoryId"
        />
        <v-btn
          v-else
          class="activate-category"
          variant="text"
          @click="categories.setActive(group.category.categoryId, true)"
        >
          {{ t('admin.categories.activate') }}
        </v-btn>
      </div>
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
            :error-text="itemRefusal"
            @save="save"
          />
        </v-expand-transition>
      </v-card>
    </section>

    <div class="d-flex ga-2">
      <v-btn class="new-item" color="primary" @click="startCreating">
        {{ t('admin.items.new') }}
      </v-btn>
      <v-btn class="new-category" color="primary" variant="tonal" @click="startCreatingCategory">
        {{ t('admin.categories.new') }}
      </v-btn>
    </div>

    <NewItemDialog
      v-if="isCreating"
      :stations="stations.stations"
      :error-text="itemRefusal"
      @save="save"
      @cancel="stopCreating"
    />
    <CategoryDialog
      v-if="isCategoryDialogOpen"
      :key="renamedCategory?.categoryId ?? 'new'"
      :category="renamedCategory"
      :error-text="categoryRefusal"
      @save="saveCategory"
      @cancel="stopEditingCategory"
    />
    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.items.deactivateTitle')"
      :body="t('admin.items.deactivateBody')"
      :confirm-label="t('admin.items.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
    <ConfirmDialog
      v-if="askingAboutCategoryId !== null"
      :title="t('admin.categories.deactivateTitle')"
      :body="t('admin.categories.deactivateBody')"
      :confirm-label="t('admin.categories.deactivateConfirm')"
      @confirm="deactivateCategory"
      @cancel="askingAboutCategoryId = null"
    />
  </v-container>
</template>
