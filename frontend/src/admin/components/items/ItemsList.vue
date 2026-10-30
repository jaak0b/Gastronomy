<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminCategoryView, AdminItemView, MoveDirection } from '../../../shared/api/generatedSchemas'
import { combineReleases } from '../../../shared/core/combineReleases'
import { showsTheEmptyListText } from '../../../shared/core/emptyList'
import { groupByCategorySortingItemsByName } from '../../../shared/core/grouping'
import { numberRowsAcrossGroups } from '../../core/alternatingRows'
import { useAdminCategoriesStore, type AdminCategoryDraft } from '../../stores/categories'
import { useAdminFestivalsStore } from '../../stores/festivals'
import { useAdminIngredientsStore } from '../../stores/ingredients'
import { useAdminUnitsStore } from '../../stores/units'
import { useAdminItemsStore, type AdminItemDraft } from '../../stores/items'
import { useEditSession } from '../../composables/useEditSession'
import BaseHeadingWithCheckbox from '../BaseHeadingWithCheckbox.vue'
import CategoryDialog from '../categories/CategoryDialog.vue'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import BaseEmptyListText from '../../../shared/components/BaseEmptyListText.vue'
import BaseRefusalAlert from '../../../shared/components/BaseRefusalAlert.vue'
import ItemDialog from './ItemDialog.vue'
import ItemsListCategorySection from './ItemsListCategorySection.vue'
import ItemIngredientsDialog from './ItemIngredientsDialog.vue'

const { t } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const festivals = useAdminFestivalsStore()
const ingredients = useAdminIngredientsStore()
const units = useAdminUnitsStore()
const recipeItemId = ref<string | null>(null)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const askingAboutCategoryId = ref<string | null>(null)
const itemSession = useEditSession<AdminItemView, AdminItemDraft>({
  saveThrough: (draft) => items.save(draft),
})
const categorySession = useEditSession<AdminCategoryView, AdminCategoryDraft, AdminCategoryView | null>({
  saveThrough: (draft, category) =>
    category === null
      ? categories.create(draft)
      : categories.save({ categoryId: category.categoryId, ...draft }),
  afterSaving: forgetRefusals,
})
const {
  isOpen: isItemDialogOpen,
  edited: editedItem,
  refusalText: itemRefusalText,
} = itemSession
const {
  isOpen: isCategoryDialogOpen,
  edited: editedCategory,
  refusalText: categoryRefusalText,
} = categorySession
let stopListening: (() => void) | null = null

const shownItems = computed(() =>
  items.items.filter((item) => showsDeactivated.value || item.isActive),
)

const groups = computed(() =>
  numberRowsAcrossGroups(
    groupByCategorySortingItemsByName(
      categories.categories,
      shownItems.value,
      (category) => category.categoryId,
      (item) => item.categoryId,
      (item) => item.name,
    ),
  ),
)

const showsNoCategories = computed(() =>
  showsTheEmptyListText(categories, categories.categories.length),
)

const itemCountsByCategory = computed(() => {
  const counts = new Map<string, number>()
  for (const item of items.items) {
    counts.set(item.categoryId, (counts.get(item.categoryId) ?? 0) + 1)
  }
  return counts
})

function categoryHasNoItems(categoryId: string): boolean {
  return showsTheEmptyListText(items, itemCountsByCategory.value.get(categoryId) ?? 0)
}

const recipeItem = computed(
  () => items.items.find((item) => item.itemId === recipeItemId.value) ?? null,
)

function readItemsForTheRunningFestival(): Promise<void> {
  const festivalId = festivals.runningFestival?.festivalId ?? null
  return festivalId === null ? items.load() : items.loadAtTheFestival(festivalId)
}

function forgetRefusals(): void {
  itemSession.forgetRefusal()
  categorySession.forgetRefusal()
}

function startEditingItem(item: AdminItemView): void {
  categorySession.forgetRefusal()
  itemSession.openForEdit(item)
}

function startCreatingItem(): void {
  categorySession.forgetRefusal()
  itemSession.openForCreate()
}

function stopEditingItem(): void {
  forgetRefusals()
  itemSession.close()
}

async function deactivate(): Promise<void> {
  const itemId = askingAboutId.value
  askingAboutId.value = null
  if (itemId !== null) {
    itemSession.forgetRefusal()
    itemSession.showRefusalOf(await items.setActive(itemId, false))
  }
}

async function reactivate(itemId: string): Promise<void> {
  itemSession.forgetRefusal()
  itemSession.showRefusalOf(await items.setActive(itemId, true))
}

async function moveCategory(categoryId: string, direction: MoveDirection): Promise<void> {
  categorySession.forgetRefusal()
  categorySession.showRefusalOf(await categories.move(categoryId, direction))
}

function startCreatingCategory(): void {
  itemSession.forgetRefusal()
  categorySession.openForCreate()
}

function startRenamingCategory(category: AdminCategoryView): void {
  itemSession.forgetRefusal()
  categorySession.openForEdit(category)
}

function stopEditingCategory(): void {
  forgetRefusals()
  categorySession.close()
}

async function activateCategory(categoryId: string): Promise<void> {
  categorySession.forgetRefusal()
  categorySession.showRefusalOf(await categories.setActive(categoryId, true))
}

async function deactivateCategory(): Promise<void> {
  const categoryId = askingAboutCategoryId.value
  askingAboutCategoryId.value = null
  if (categoryId !== null) {
    categorySession.forgetRefusal()
    categorySession.showRefusalOf(await categories.setActive(categoryId, false))
  }
}

watch(
  () => festivals.runningFestival?.festivalId ?? null,
  () => {
    void readItemsForTheRunningFestival()
  },
)

onMounted(async () => {
  stopListening = combineReleases(
    categories.listen(),
    items.listen(),
    festivals.listen(),
    ingredients.listen(),
    units.listen(),
  )
  await categories.load()
  await festivals.load()
  await readItemsForTheRunningFestival()
  await Promise.all([ingredients.load(), units.load()])
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-items" data-test="admin-items">
    <BaseHeadingWithCheckbox
      v-model="showsDeactivated"
      class="admin-heading admin-section-gap"
      :checkbox-label="t('admin.common.actions.showDeactivated')"
      checkbox-test-name="show-deactivated"
    >
      <h1 class="app-page-title">{{ t('admin.items.title') }}</h1>
    </BaseHeadingWithCheckbox>

    <BaseRefusalAlert
      v-if="itemRefusalText !== null && !isItemDialogOpen"
      class="admin-section-gap"
      data-test="item-refusal"
      :text="itemRefusalText"
    />
    <BaseRefusalAlert
      v-if="categoryRefusalText !== null && !isCategoryDialogOpen"
      class="admin-section-gap"
      data-test="category-refusal"
      :text="categoryRefusalText"
    />

    <BaseEmptyListText v-if="showsNoCategories" class="admin-section-gap" />

    <ItemsListCategorySection
      v-for="group in groups"
      :key="group.category.categoryId"
      :category="group.category"
      :rows="group.rows"
      :shows-empty-list-text="categoryHasNoItems(group.category.categoryId)"
      :is-a-festival-running="festivals.runningFestival !== null"
      @rename-category="startRenamingCategory(group.category)"
      @move-category="(direction) => moveCategory(group.category.categoryId, direction)"
      @deactivate-category="askingAboutCategoryId = group.category.categoryId"
      @activate-category="activateCategory(group.category.categoryId)"
      @edit-ingredients="(item) => (recipeItemId = item.itemId)"
      @edit-item="startEditingItem"
      @deactivate-item="(item) => (askingAboutId = item.itemId)"
      @reactivate-item="(item) => reactivate(item.itemId)"
    />

    <div class="d-flex ga-2 admin-block-gap">
      <v-btn class="new-item" data-test="new-item" color="primary" @click="startCreatingItem">
        {{ t('admin.items.actions.new') }}
      </v-btn>
      <v-btn class="new-category" data-test="new-category" color="primary" variant="tonal" @click="startCreatingCategory">
        {{ t('admin.categories.actions.new') }}
      </v-btn>
    </div>

    <ItemDialog
      v-if="isItemDialogOpen"
      :item="editedItem"
      :error-text="itemRefusalText"
      @save="itemSession.save"
      @cancel="stopEditingItem"
    />
    <ItemIngredientsDialog
      v-if="recipeItem !== null"
      :item="recipeItem"
      @close="recipeItemId = null"
    />
    <CategoryDialog
      v-if="isCategoryDialogOpen"
      :key="editedCategory?.categoryId ?? 'new'"
      :category="editedCategory"
      :error-text="categoryRefusalText"
      @save="categorySession.save"
      @cancel="stopEditingCategory"
    />
    <BaseConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.items.labels.deactivateTitle')"
      :confirm-label="t('admin.items.actions.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
    <BaseConfirmDialog
      v-if="askingAboutCategoryId !== null"
      :title="t('admin.categories.labels.deactivateTitle')"
      :confirm-label="t('admin.categories.actions.deactivateConfirm')"
      @confirm="deactivateCategory"
      @cancel="askingAboutCategoryId = null"
    />
  </v-container>
</template>
