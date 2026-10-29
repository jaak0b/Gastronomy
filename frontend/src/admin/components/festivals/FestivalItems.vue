<script setup lang="ts">
import { computed, ref, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminCategoryView, AdminItemView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { compareNames } from '../../../shared/core/compareNames'
import { groupByCategorySortingItemsByName } from '../../../shared/core/grouping'
import { numberRowsAcrossGroups } from '../../core/alternatingRows'
import { useAdminCategoriesStore, type AdminCategoryDraft } from '../../stores/categories'
import { useAdminItemsStore, type AdminItemDraft } from '../../stores/items'
import { useAdminStationsStore } from '../../stores/stations'
import { useEditSession } from '../../composables/useEditSession'
import { useFestivalItemRows } from '../../composables/useFestivalItemRows'
import BaseRefusalAlert from '../../../shared/components/BaseRefusalAlert.vue'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseListRow from '../BaseListRow.vue'
import BaseRemoveConfirmDialog from '../BaseRemoveConfirmDialog.vue'
import BaseSearchAddLine from '../BaseSearchAddLine.vue'
import BaseTrashButton from '../BaseTrashButton.vue'
import CategoryDialog from '../categories/CategoryDialog.vue'
import CategoryLabel from '../../../shared/components/CategoryLabel.vue'
import ItemDialog from '../items/ItemDialog.vue'
import FestivalPlacementDialog from './FestivalPlacementDialog.vue'
import StationSelect from './StationSelect.vue'

const props = defineProps<{ festivalId: string; isRunning: boolean }>()

const { t } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const stations = useAdminStationsStore()
const chosenItemId = ref<string | null>(null)
const itemToPlace = ref<AdminItemView | null>(null)
const removedItem = ref<AdminItemView | null>(null)
const itemSession = useEditSession<AdminItemView, AdminItemDraft, AdminItemView | null>({
  saveThrough: (draft, item) => (item === null ? items.create(draft) : items.save(draft)),
  afterSaving: (saved, mode) => {
    switch (mode) {
      case 'create':
        if (saved === null) {
          throw new Error('The laptop accepted a new item without returning it.')
        }
        startPlacing(saved)
        return
      case 'edit':
        return
      default:
        assertNever(mode)
    }
  },
})
const categorySession = useEditSession<AdminCategoryView, AdminCategoryDraft, AdminCategoryView | null>({
  saveThrough: (draft, category) =>
    category === null
      ? categories.create(draft)
      : categories.save({ categoryId: category.categoryId, ...draft }),
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

const {
  itemsAtTheFestival,
  rowShownFor,
  rowRefusalText,
  priceIsUnreadable,
  typePrice,
  save,
  changeStations,
  setSoldOut,
  removeFromTheFestival,
} = useFestivalItemRows(toRef(props, 'festivalId'))

const festivalStations = computed(() =>
  stations.stations.filter((station) => station.isAtFestival && station.isActive),
)

const groups = computed(() =>
  numberRowsAcrossGroups(
    groupByCategorySortingItemsByName(
      categories.categories,
      itemsAtTheFestival.value,
      (category) => category.categoryId,
      (item) => item.categoryId,
      (item) => item.name,
    ).filter((group) => group.items.length > 0),
  ),
)

const stillToAdd = computed(() =>
  items.items
    .filter((item) => item.isActive && item.atTheFestival === null)
    .sort((left, right) => compareNames(left.name, right.name)),
)

const itemsUnavailableReason = computed(() =>
  stillToAdd.value.length === 0 ? t('admin.common.messages.noItemsAvailable') : null,
)

const chosenItem = computed(
  () => items.items.find((item) => item.itemId === chosenItemId.value) ?? null,
)

function priceMessagesOf(itemId: string): string[] {
  return priceIsUnreadable(itemId) ? [t('admin.items.errors.priceInvalid')] : []
}

async function changeSoldOut(item: AdminItemView, switchedOn: boolean | null): Promise<void> {
  await setSoldOut(item, switchedOn === true)
}

function startPlacing(item: AdminItemView): void {
  itemToPlace.value = item
}

function stopPlacing(): void {
  itemToPlace.value = null
}

function add(): void {
  const item = chosenItem.value
  if (item === null) {
    return
  }
  startPlacing(item)
  chosenItemId.value = null
}

async function remove(): Promise<void> {
  const item = removedItem.value
  removedItem.value = null
  if (item === null) {
    return
  }
  await removeFromTheFestival(item)
}
</script>

<template>
  <section class="festival-items admin-section-gap" data-test="festival-items">
    <v-card variant="outlined">
      <div class="admin-card-body">
        <h2 class="section-heading app-section-title mb-3" data-test="section-heading">{{ t('admin.items.title') }}</h2>

        <div v-if="festivalStations.length === 0" class="festival-item-placeholder" data-test="festival-item-placeholder">
          <div class="item-line d-flex align-center flex-wrap admin-row">
            <span class="needs-a-station" data-test="needs-a-station">
              {{ t('admin.festivals.messages.needsAStationFirst') }}
            </span>
          </div>
        </div>

        <template v-else>
          <section
            v-for="group in groups"
            :key="group.category.categoryId"
            class="category-section"
          >
            <div class="category-heading d-flex align-center flex-wrap ga-2 mb-1 mt-3">
              <CategoryLabel
                class="category-name"
                data-test="category-name"
                :category="group.category"
                :heading-level="3"
              />
              <BaseEditButton
                class="edit-category"
                data-test="edit-category"
                @click="categorySession.openForEdit(group.category)"
              />
            </div>
            <BaseListRow
              v-for="{ item, position } in group.rows"
              :key="item.itemId"
              class="festival-item-row"
              data-test="festival-item-row"
              :data-test-id="item.itemId"
              :position="position"
              :name="item.name"
              name-test-name="item-name"
              :refusal-text="rowRefusalText(item.itemId)"
            >
              <template #facts>
                <v-chip v-if="!item.isActive" class="deactivated" data-test="deactivated" color="grey">
                  {{ t('admin.common.labels.deactivated') }}
                </v-chip>
                <v-text-field
                  class="price-field"
                  data-test="price-field"
                  inputmode="decimal"
                  hide-details="auto"
                  :model-value="rowShownFor(item.itemId).price.edited"
                  :label="t('admin.items.labels.price')"
                  :error="priceIsUnreadable(item.itemId)"
                  :error-messages="priceMessagesOf(item.itemId)"
                  @update:model-value="(typed: string) => typePrice(item.itemId, typed)"
                  @blur="save(item.itemId)"
                  @keyup.enter="save(item.itemId)"
                />
                <StationSelect
                  :stations="festivalStations"
                  :selected-station-ids="rowShownFor(item.itemId).stations.edited"
                  :error-text="null"
                  @select="(stationIds: string[]) => changeStations(item.itemId, stationIds)"
                />
              </template>
              <template #actions>
                <v-switch
                  :key="`${item.itemId}-${item.atTheFestival.isAvailable}-${rowShownFor(item.itemId).soldOutSwitchRenderKey}`"
                  class="sold-out-switch flex-grow-0"
                  data-test="sold-out-switch"
                  color="primary"
                  hide-details
                  :model-value="!item.atTheFestival.isAvailable"
                  :label="t('common.labels.soldOut')"
                  @update:model-value="(switchedOn: boolean | null) => changeSoldOut(item, switchedOn)"
                />
                <BaseEditButton class="edit-item" data-test="edit-item" @click="itemSession.openForEdit(item)" />
                <BaseActionWithRefusalReason
                  class="remove-item-wrapper"
                  data-test="remove-item-wrapper"
                  :refusal-reason="isRunning ? t('errors.admin.festivals.itemStaysOnTheMenuWhileTheFestivalRuns') : null"
                >
                  <template #default="{ refused }">
                    <BaseTrashButton
                      class="remove-item"
                      data-test="remove-item"
                      :disabled="refused"
                      @click="removedItem = item"
                    />
                  </template>
                </BaseActionWithRefusalReason>
              </template>
            </BaseListRow>
          </section>

          <BaseSearchAddLine
            v-model:chosen-id="chosenItemId"
            class="admin-block-gap"
            :entries="stillToAdd"
            entry-title="name"
            entry-value="itemId"
            :label="t('admin.festivals.labels.itemName')"
            :unavailable-reason="itemsUnavailableReason"
            :add-label="t('admin.festivals.actions.addItem')"
            search-test-name="item-search"
            add-test-name="add-item"
            @add="add"
          >
            <v-btn class="new-item" data-test="new-item" variant="text" @click="itemSession.openForCreate()">
              {{ t('admin.items.actions.new') }}
            </v-btn>
          </BaseSearchAddLine>
          <BaseRefusalAlert
            v-if="itemRefusalText !== null && !isItemDialogOpen"
            class="mt-3"
            :text="itemRefusalText"
          />
        </template>
      </div>
    </v-card>

    <CategoryDialog
      v-if="isCategoryDialogOpen"
      :category="editedCategory"
      :error-text="categoryRefusalText"
      @save="categorySession.save"
      @cancel="categorySession.close"
    />

    <ItemDialog
      v-if="isItemDialogOpen"
      :item="editedItem"
      :error-text="itemRefusalText"
      @save="itemSession.save"
      @cancel="itemSession.close"
    />

    <FestivalPlacementDialog
      v-if="itemToPlace !== null"
      :festival-id="festivalId"
      :item="itemToPlace"
      :stations="festivalStations"
      @placed="stopPlacing"
      @cancel="stopPlacing"
    />

    <BaseRemoveConfirmDialog
      v-if="removedItem !== null"
      :title="t('admin.festivals.labels.removeItemTitle')"
      :body="t('admin.festivals.messages.removeItemBody')"
      @confirm="remove"
      @cancel="removedItem = null"
    />
  </section>
</template>

<style scoped>
.festival-item-row :deep(.row-name) {
  flex: 0 1 12rem;
}

.festival-item-row .price-field {
  flex: 0 0 9rem;
}

.festival-item-row .station-select-field {
  flex: 0 1 auto;
}
</style>
