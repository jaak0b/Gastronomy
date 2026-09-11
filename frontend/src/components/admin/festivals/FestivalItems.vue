<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { adminMessage, type AdminErrorMessage } from '../../../core/adminErrorMessage'
import type { AppLanguage } from '../../../core/apiTypes'
import { groupByCategory } from '../../../core/grouping'
import { letteringColourOn } from '../../../core/letteringColour'
import { formatEuroInput, parseEuroInput } from '../../../core/money'
import { useAdminCategoriesStore } from '../../../stores/admin/categories'
import {
  useAdminItemsStore,
  type AdminItem,
  type AdminItemDraft,
} from '../../../stores/admin/items'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import ConfirmDialog from '../ConfirmDialog.vue'
import ItemForm from '../items/ItemForm.vue'
import StationChips from '../items/StationChips.vue'
import { useRefusalText } from '../refusalText'

interface Placement {
  priceText: string
  stationIds: string[]
}

interface ItemRow {
  priceText: string
  stationIds: string[]
  sentPriceText: string
  sentStationIds: string[]
  onItsWay: Placement | null
  laptopAnswers: number
}

const props = defineProps<{ festivalId: string }>()

const { t, locale } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const stations = useAdminStationsStore()
const rows = ref(new Map<string, ItemRow>())
const justAddedItemIds = ref<string[]>([])
const chosenItemId = ref<string | null>(null)
const isCreating = ref(false)
const removedItem = ref<AdminItem | null>(null)
const refusedItemId = ref<string | null>(null)
const ownRefusal = ref<AdminErrorMessage | null>(null)

const refusalText = useRefusalText([() => items.errorMessage, () => ownRefusal.value])

const festivalStations = computed(() =>
  stations.stations.filter((station) => station.isAtTheFestival && station.isActive),
)

const itemsAtTheFestival = computed(() =>
  items.items.filter(
    (item) => item.atTheFestival !== null || justAddedItemIds.value.includes(item.itemId),
  ),
)

const groups = computed(() =>
  groupByCategory(
    categories.categories,
    itemsAtTheFestival.value,
    (category) => category.categoryId,
    (item) => item.categoryId,
    (item) => item.name,
  ).filter((group) => group.items.length > 0),
)

const tintedItemIds = computed(() => {
  const tinted = new Set<string>()
  let position = 0
  for (const group of groups.value) {
    for (const item of group.items) {
      if (position % 2 === 1) {
        tinted.add(item.itemId)
      }
      position += 1
    }
  }
  return tinted
})

const stillToAdd = computed(() =>
  [
    ...items.items.filter(
      (item) =>
        item.isActive
        && item.atTheFestival === null
        && !justAddedItemIds.value.includes(item.itemId),
    ),
  ].sort((left, right) => left.name.localeCompare(right.name)),
)

function rowFrom(item: AdminItem): ItemRow {
  const priceText = formatEuroInput(
    item.atTheFestival?.priceCents ?? null,
    locale.value as AppLanguage,
  )
  const stationIds = [...(item.atTheFestival?.stationIds ?? [])]
  return {
    priceText,
    stationIds,
    sentPriceText: priceText,
    sentStationIds: [...stationIds],
    onItsWay: null,
    laptopAnswers: laptopAnswersAbout(item.itemId),
  }
}

function laptopAnswersAbout(itemId: string): number {
  return rows.value.get(itemId)?.laptopAnswers ?? 0
}

function isBeingEdited(row: ItemRow): boolean {
  return row.onItsWay !== null || !matchesTheLaptop(row, row.sentPriceText, row.sentStationIds)
}

watch(
  itemsAtTheFestival,
  (listed) => {
    const next = new Map<string, ItemRow>()
    for (const item of listed) {
      const known = rows.value.get(item.itemId)
      next.set(item.itemId, known !== undefined && isBeingEdited(known) ? known : rowFrom(item))
    }
    rows.value = next
  },
  { immediate: true },
)

function rowOf(itemId: string): ItemRow {
  return (
    rows.value.get(itemId)
    ?? {
      priceText: '',
      stationIds: [],
      sentPriceText: '',
      sentStationIds: [],
      onItsWay: null,
      laptopAnswers: 0,
    }
  )
}

function namesTheSameStations(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((stationId) => right.includes(stationId))
}

function matchesTheLaptop(row: ItemRow, priceText: string, stationIds: string[]): boolean {
  return row.priceText === priceText && namesTheSameStations(row.stationIds, stationIds)
}

function isAlreadyAtTheLaptop(row: ItemRow): boolean {
  const onItsWay = row.onItsWay
  if (onItsWay !== null) {
    return matchesTheLaptop(row, onItsWay.priceText, onItsWay.stationIds)
  }
  return matchesTheLaptop(row, row.sentPriceText, row.sentStationIds)
}

function theLaptopHoldsTheRow(itemId: string): boolean {
  return !justAddedItemIds.value.includes(itemId)
}

function refuse(itemId: string, messageKey: string): void {
  refusedItemId.value = itemId
  ownRefusal.value = adminMessage(messageKey)
}

function typePrice(itemId: string, typed: string): void {
  const row = rows.value.get(itemId)
  if (row !== undefined) {
    row.priceText = typed
  }
}

function priceIsUnreadable(itemId: string): boolean {
  const typed = rowOf(itemId).priceText
  return typed.trim().length > 0 && parseEuroInput(typed) === null
}

function priceTheLaptopCanTake(itemId: string, row: ItemRow): number | null {
  const priceCents = parseEuroInput(row.priceText)
  if (priceCents === null) {
    refuse(itemId, 'admin.itemPriceOutOfRange')
    return null
  }
  if (row.stationIds.length === 0) {
    refuse(itemId, 'admin.itemNeedsAStation')
    return null
  }
  return priceCents
}

async function save(itemId: string): Promise<void> {
  const row = rows.value.get(itemId)
  if (row === undefined) {
    return
  }
  if (isAlreadyAtTheLaptop(row)) {
    return
  }
  if (priceIsUnreadable(itemId)) {
    return
  }
  if (priceTheLaptopCanTake(itemId, row) === null) {
    return
  }
  refusedItemId.value = itemId
  ownRefusal.value = null
  if (row.onItsWay !== null) {
    return
  }
  await sendUntilTheLaptopHasTheRow(itemId, row)
}

async function sendUntilTheLaptopHasTheRow(itemId: string, row: ItemRow): Promise<void> {
  while (!matchesTheLaptop(row, row.sentPriceText, row.sentStationIds)) {
    const priceCents = priceTheLaptopCanTake(itemId, row)
    if (priceCents === null) {
      return
    }
    const onItsWay: Placement = { priceText: row.priceText, stationIds: [...row.stationIds] }
    row.onItsWay = onItsWay
    const placed = await items.putAtTheFestival(props.festivalId, itemId, {
      priceCents,
      stationIds: [...onItsWay.stationIds],
    })
    row.laptopAnswers += 1
    if (placed) {
      row.sentPriceText = onItsWay.priceText
      row.sentStationIds = [...onItsWay.stationIds]
      justAddedItemIds.value = justAddedItemIds.value.filter((id) => id !== itemId)
      row.onItsWay = null
      continue
    }
    if (theLaptopHoldsTheRow(itemId)) {
      row.priceText = row.sentPriceText
      row.stationIds = [...row.sentStationIds]
    }
    row.onItsWay = null
    return
  }
}

async function toggleStation(itemId: string, stationId: string): Promise<void> {
  const row = rows.value.get(itemId)
  if (row === undefined) {
    return
  }
  if (priceIsUnreadable(itemId)) {
    refuse(itemId, 'admin.itemPriceOutOfRange')
    return
  }
  const isOnTheItem = row.stationIds.includes(stationId)
  if (isOnTheItem && row.stationIds.length === 1) {
    refuse(itemId, 'admin.itemNeedsAStation')
    return
  }
  row.stationIds = isOnTheItem
    ? row.stationIds.filter((id) => id !== stationId)
    : [...row.stationIds, stationId]
  await save(itemId)
}

async function setSoldOut(item: AdminItem, isSoldOut: boolean): Promise<void> {
  refusedItemId.value = item.itemId
  ownRefusal.value = null
  await items.setAvailability(props.festivalId, item.itemId, !isSoldOut)
  const row = rows.value.get(item.itemId)
  if (row !== undefined) {
    row.laptopAnswers += 1
  }
}

function add(): void {
  const itemId = chosenItemId.value
  if (itemId === null) {
    return
  }
  if (!justAddedItemIds.value.includes(itemId)) {
    justAddedItemIds.value = [...justAddedItemIds.value, itemId]
  }
  chosenItemId.value = null
}

function startCreating(): void {
  items.forgetError()
  ownRefusal.value = null
  refusedItemId.value = null
  isCreating.value = true
}

function stopCreating(): void {
  items.forgetError()
  isCreating.value = false
}

async function create(draft: AdminItemDraft): Promise<void> {
  const itemId = await items.create(draft)
  if (itemId === null) {
    return
  }
  isCreating.value = false
  chosenItemId.value = itemId
}

async function remove(): Promise<void> {
  const item = removedItem.value
  removedItem.value = null
  if (item === null) {
    return
  }
  refusedItemId.value = item.itemId
  ownRefusal.value = null
  justAddedItemIds.value = justAddedItemIds.value.filter((id) => id !== item.itemId)
  await items.removeFromTheFestival(props.festivalId, item.itemId)
}
</script>

<template>
  <section class="festival-items mb-4">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3">{{ t('admin.items.title') }}</h2>

        <div v-if="festivalStations.length === 0" class="festival-item-placeholder">
          <div class="item-line d-flex align-center flex-wrap ga-3 py-2 px-3">
            <span class="needs-a-station text-body-1">
              {{ t('admin.festival.needsAStationFirst') }}
            </span>
          </div>
        </div>

        <template v-else>
          <section
            v-for="group in groups"
            :key="group.category.categoryId"
            class="category-section"
          >
            <h3
              class="category-name text-subtitle-1 font-weight-bold px-3 py-1 rounded d-inline-block mb-1 mt-3"
              :style="{
                backgroundColor: group.category.colourHex,
                color: letteringColourOn(group.category.colourHex),
              }"
            >
              {{ group.category.name }}
            </h3>
            <div
              v-for="item in group.items"
              :key="item.itemId"
              class="festival-item-row"
              :class="{ 'tinted-row': tintedItemIds.has(item.itemId) }"
            >
              <div class="item-line d-flex align-center flex-wrap ga-3 py-2 px-3">
                <span class="name text-body-1">{{ item.name }}</span>
                <v-chip v-if="!item.isActive" class="deactivated" size="small" color="grey">
                  {{ t('admin.deactivated') }}
                </v-chip>
                <v-text-field
                  class="price-field"
                  density="compact"
                  inputmode="decimal"
                  hide-details="auto"
                  :model-value="rowOf(item.itemId).priceText"
                  :label="t('admin.items.price')"
                  :error="priceIsUnreadable(item.itemId)"
                  :error-messages="
                    priceIsUnreadable(item.itemId) ? [t('admin.items.priceInvalid')] : []
                  "
                  @update:model-value="(typed: string) => typePrice(item.itemId, typed)"
                  @blur="save(item.itemId)"
                  @keyup.enter="save(item.itemId)"
                />
                <StationChips
                  :stations="festivalStations"
                  :selected-station-ids="rowOf(item.itemId).stationIds"
                  @toggle="(stationId: string) => toggleStation(item.itemId, stationId)"
                />
                <v-spacer />
                <v-switch
                  v-if="item.atTheFestival !== null"
                  :key="`${item.itemId}-${item.atTheFestival.isAvailable}-${rowOf(item.itemId).laptopAnswers}`"
                  class="sold-out-switch flex-grow-0"
                  density="compact"
                  color="primary"
                  hide-details
                  :model-value="!item.atTheFestival.isAvailable"
                  :label="t('admin.items.soldOut')"
                  @update:model-value="(value: boolean | null) => setSoldOut(item, value === true)"
                />
                <v-btn class="remove-item" variant="text" @click="removedItem = item">
                  {{ t('admin.festival.remove') }}
                </v-btn>
              </div>
              <v-alert
                v-if="refusalText !== null && refusedItemId === item.itemId"
                class="refusal mb-2"
                type="warning"
                variant="tonal"
              >
                {{ refusalText }}
              </v-alert>
            </div>
          </section>

          <div class="add-item-line d-flex align-center flex-wrap ga-3 mt-4">
            <v-autocomplete
              v-model="chosenItemId"
              class="item-search flex-grow-1"
              :items="stillToAdd"
              item-title="name"
              item-value="itemId"
              density="compact"
              hide-details
              :label="t('admin.festival.itemName')"
              :no-data-text="t('admin.festival.noItemsToAdd')"
            />
            <v-btn
              class="add-item"
              color="primary"
              variant="tonal"
              :disabled="chosenItemId === null"
              @click="add"
            >
              {{ t('admin.festival.addItem') }}
            </v-btn>
            <v-btn class="new-item" variant="text" @click="startCreating">
              {{ t('admin.items.new') }}
            </v-btn>
          </div>
          <v-alert
            v-if="refusalText !== null && refusedItemId === null && !isCreating"
            class="refusal mt-3"
            type="warning"
            variant="tonal"
          >
            {{ refusalText }}
          </v-alert>
        </template>
      </div>
    </v-card>

    <v-dialog v-if="isCreating" :model-value="true" max-width="560" persistent scrollable>
      <v-card class="new-item-dialog" role="dialog" aria-modal="true">
        <v-card-title class="new-item-title">{{ t('admin.items.new') }}</v-card-title>
        <ItemForm
          :item="null"
          :error-text="refusalText"
          is-cancellable
          @save="create"
          @cancel="stopCreating"
        />
      </v-card>
    </v-dialog>

    <ConfirmDialog
      v-if="removedItem !== null"
      :title="t('admin.festival.removeItemTitle')"
      :body="t('admin.festival.removeItemBody')"
      :confirm-label="t('admin.festival.remove')"
      @confirm="remove"
      @cancel="removedItem = null"
    />
  </section>
</template>

<style scoped>
.festival-item-row + .festival-item-row {
  border-top: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
}

.festival-item-row.tinted-row {
  background-color: rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 6px;
}

.festival-item-row .name {
  flex: 0 1 12rem;
}

.festival-item-row .price-field {
  flex: 0 0 9rem;
}

.festival-item-row .station-chips {
  flex: 0 1 auto;
}
</style>
