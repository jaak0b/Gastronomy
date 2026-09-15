<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { refusalFrom, type AdminActionResult } from '../../../core/adminActionResult'
import type { AdminErrorMessage } from '../../../core/adminErrorMessage'
import type { AdminCategory } from '../../../core/apiTypes'
import { assertNever } from '../../../core/assertNever'
import { groupByCategory } from '../../../core/grouping'
import { letteringColourOn } from '../../../core/letteringColour'
import {
  useAdminCategoriesStore,
  type AdminCategoryDraft,
  type CategoryMoveDirection,
} from '../../../stores/admin/categories'
import { useAdminItemsStore, type AdminItemDraft } from '../../../stores/admin/items'
import CategoryDialog from '../categories/CategoryDialog.vue'
import ConfirmDialog from '../ConfirmDialog.vue'
import { useRefusalText } from '../refusalText'
import ItemForm from './ItemForm.vue'
import NewItemDialog from './NewItemDialog.vue'

const { t } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const askingAboutCategoryId = ref<string | null>(null)
const renamedCategory = ref<AdminCategory | null>(null)
const isCreatingCategory = ref(false)
const itemRefusal = ref<AdminErrorMessage | null>(null)
const categoryRefusal = ref<AdminErrorMessage | null>(null)
let stopListening: (() => void) | null = null

const shownItems = computed(() =>
  items.items.filter((item) => showsDeactivated.value || item.isActive),
)

const groups = computed(() =>
  groupByCategory(
    categories.categories,
    shownItems.value,
    (category) => category.categoryId,
    (item) => item.categoryId,
    (item) => item.name,
  ),
)

const itemRefusalText = useRefusalText(itemRefusal)
const categoryRefusalText = useRefusalText(categoryRefusal)
const isCategoryDialogOpen = computed(
  () => isCreatingCategory.value || renamedCategory.value !== null,
)

function noteItem(result: AdminActionResult<unknown>): void {
  const message = refusalFrom(result)
  if (message !== null) {
    itemRefusal.value = message
  }
}

function noteCategory(result: AdminActionResult<unknown>): void {
  const message = refusalFrom(result)
  if (message !== null) {
    categoryRefusal.value = message
  }
}

async function save(item: AdminItemDraft): Promise<void> {
  itemRefusal.value = null
  const saved = await items.save(item)
  switch (saved.kind) {
    case 'ok':
      editingId.value = null
      isCreating.value = false
      return
    case 'failed':
      itemRefusal.value = saved.message
      return
    default:
      assertNever(saved)
  }
}

function forgetRefusals(): void {
  itemRefusal.value = null
  categoryRefusal.value = null
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
    itemRefusal.value = null
    noteItem(await items.setActive(itemId, false))
  }
}

async function reactivate(itemId: string): Promise<void> {
  itemRefusal.value = null
  noteItem(await items.setActive(itemId, true))
}

async function moveCategory(categoryId: string, direction: CategoryMoveDirection): Promise<void> {
  categoryRefusal.value = null
  noteCategory(await categories.move(categoryId, direction))
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
  categoryRefusal.value = null
  const category = renamedCategory.value
  const saved =
    category === null
      ? await categories.create(draft)
      : await categories.save({ categoryId: category.categoryId, ...draft })
  switch (saved.kind) {
    case 'ok':
      stopEditingCategory()
      return
    case 'failed':
      categoryRefusal.value = saved.message
      return
    default:
      assertNever(saved)
  }
}

async function activateCategory(categoryId: string): Promise<void> {
  categoryRefusal.value = null
  noteCategory(await categories.setActive(categoryId, true))
}

async function deactivateCategory(): Promise<void> {
  const categoryId = askingAboutCategoryId.value
  askingAboutCategoryId.value = null
  if (categoryId !== null) {
    categoryRefusal.value = null
    noteCategory(await categories.setActive(categoryId, false))
  }
}

function listenToTheLaptop(): () => void {
  const releases = [categories.listen(), items.listen()]
  return () => {
    for (const release of releases) {
      release()
    }
  }
}

onMounted(async () => {
  stopListening = listenToTheLaptop()
  await categories.load()
  await items.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-items">
    <div class="admin-heading d-flex align-center flex-wrap justify-space-between ga-2 mb-4">
      <h1 class="text-h5">{{ t('admin.items.title') }}</h1>
      <v-checkbox
        v-model="showsDeactivated"
        class="show-deactivated"
        density="compact"
        hide-details
        :label="t('admin.showDeactivated')"
      />
    </div>

    <v-alert
      v-if="itemRefusalText !== null && editingId === null && !isCreating"
      class="refusal mb-4"
      type="warning"
      variant="tonal"
    >
      {{ itemRefusalText }}
    </v-alert>
    <v-alert
      v-if="categoryRefusalText !== null && !isCategoryDialogOpen"
      class="refusal mb-4"
      type="warning"
      variant="tonal"
    >
      {{ categoryRefusalText }}
    </v-alert>
    <v-alert
      v-if="items.loadFailed || categories.loadFailed"
      class="error"
      type="error"
      variant="tonal"
    >
      {{ t('admin.loadFailed') }}
    </v-alert>

    <section v-for="group in groups" :key="group.category.categoryId" class="category-section">
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
          @click="moveCategory(group.category.categoryId, 'up')"
        />
        <v-btn
          class="move-category-down"
          icon="mdi-arrow-down"
          variant="text"
          :aria-label="t('admin.categories.moveDown')"
          @click="moveCategory(group.category.categoryId, 'down')"
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
          @click="activateCategory(group.category.categoryId)"
        >
          {{ t('admin.categories.activate') }}
        </v-btn>
      </div>
      <v-card v-for="item in group.items" :key="item.itemId" class="item-row mb-2">
        <div class="item-line d-flex align-center flex-wrap ga-2 px-4 py-2">
          <span class="name text-body-1">{{ item.name }}</span>
          <v-chip v-if="!item.isActive" class="deactivated" size="small" color="grey">
            {{ t('admin.deactivated') }}
          </v-chip>
          <v-spacer />
          <v-btn class="edit" variant="text" @click="toggleEditing(item.itemId)">
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
            @click="reactivate(item.itemId)"
          >
            {{ t('admin.items.activate') }}
          </v-btn>
        </div>
        <v-expand-transition>
          <ItemForm
            v-if="editingId === item.itemId"
            :item="item"
            :error-text="itemRefusalText"
            @save="save"
          />
        </v-expand-transition>
      </v-card>
    </section>

    <div class="d-flex ga-2 mt-6">
      <v-btn class="new-item" color="primary" @click="startCreating">
        {{ t('admin.items.new') }}
      </v-btn>
      <v-btn class="new-category" color="primary" variant="tonal" @click="startCreatingCategory">
        {{ t('admin.categories.new') }}
      </v-btn>
    </div>

    <NewItemDialog
      v-if="isCreating"
      :error-text="itemRefusalText"
      @save="save"
      @cancel="stopCreating"
    />
    <CategoryDialog
      v-if="isCategoryDialogOpen"
      :key="renamedCategory?.categoryId ?? 'new'"
      :category="renamedCategory"
      :error-text="categoryRefusalText"
      @save="saveCategory"
      @cancel="stopEditingCategory"
    />
    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.items.deactivateTitle')"
      :confirm-label="t('admin.items.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
    <ConfirmDialog
      v-if="askingAboutCategoryId !== null"
      :title="t('admin.categories.deactivateTitle')"
      :confirm-label="t('admin.categories.deactivateConfirm')"
      @confirm="deactivateCategory"
      @cancel="askingAboutCategoryId = null"
    />
  </v-container>
</template>
