<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { refusalFrom, type AdminActionResult } from '../../../core/adminActionResult'
import { adminMessage, type AdminErrorMessage } from '../../../core/adminErrorMessage'
import type { AdminItem, AppLanguage } from '../../../core/apiTypes'
import { assertNever } from '../../../core/assertNever'
import { groupByCategory } from '../../../core/grouping'
import { letteringColourOn } from '../../../core/letteringColour'
import { formatEuroInput, parseEuroInput } from '../../../core/money'
import { useAdminCategoriesStore } from '../../../stores/admin/categories'
import { useAdminItemsStore, type AdminItemDraft } from '../../../stores/admin/items'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import ConfirmDialog from '../ConfirmDialog.vue'
import ItemDialog from '../items/ItemDialog.vue'
import { refusalMessageText, useRefusalText } from '../refusalText'
import FestivalPlacementDialog from './FestivalPlacementDialog.vue'
import StationSelect from './StationSelect.vue'

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
  refusal: AdminErrorMessage | null
}

type PlacedItem = AdminItem & { atTheFestival: NonNullable<AdminItem['atTheFestival']> }

const props = defineProps<{ festivalId: string; isRunning: boolean }>()

const { t, locale } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const stations = useAdminStationsStore()
const rows = ref(new Map<string, ItemRow>())
const chosenItemId = ref<string | null>(null)
const itemToPlace = ref<AdminItem | null>(null)
const isCreating = ref(false)
const removedItem = ref<AdminItem | null>(null)
const createRefusal = ref<AdminErrorMessage | null>(null)

const createRefusalText = useRefusalText(createRefusal)

const festivalStations = computed(() =>
  stations.stations.filter((station) => station.isAtTheFestival && station.isActive),
)

const itemsAtTheFestival = computed(() =>
  items.items.filter((item): item is PlacedItem => item.atTheFestival !== null),
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
  items.items
    .filter((item) => item.isActive && item.atTheFestival === null)
    .sort((left, right) => left.name.localeCompare(right.name)),
)

const chosenItem = computed(
  () => items.items.find((item) => item.itemId === chosenItemId.value) ?? null,
)

function rowFrom(item: PlacedItem): ItemRow {
  const priceText = formatEuroInput(item.atTheFestival.priceCents, locale.value as AppLanguage)
  const stationIds = [...item.atTheFestival.stationIds]
  return {
    priceText,
    stationIds,
    sentPriceText: priceText,
    sentStationIds: [...stationIds],
    onItsWay: null,
    laptopAnswers: laptopAnswersAbout(item.itemId),
    refusal: null,
  }
}

function laptopAnswersAbout(itemId: string): number {
  return rows.value.get(itemId)?.laptopAnswers ?? 0
}

function isBeingEdited(row: ItemRow): boolean {
  return (
    row.onItsWay !== null
    || row.refusal !== null
    || !matchesTheLaptop(row, row.sentPriceText, row.sentStationIds)
  )
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

function rowFor(itemId: string): ItemRow {
  return (
    rows.value.get(itemId)
    ?? {
      priceText: '',
      stationIds: [],
      sentPriceText: '',
      sentStationIds: [],
      onItsWay: null,
      laptopAnswers: 0,
      refusal: null,
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

function itemNameFor(itemId: string): string {
  return items.items.find((item) => item.itemId === itemId)?.name ?? ''
}

function refuse(
  itemId: string,
  messageKey: string,
  parameters: Record<string, string | number> = {},
): void {
  const row = rows.value.get(itemId)
  if (row !== undefined) {
    row.refusal = adminMessage(messageKey, parameters)
  }
}

function showRowRefusal(itemId: string, result: AdminActionResult<unknown>): void {
  const message = refusalFrom(result)
  if (message === null) {
    return
  }
  const row = rows.value.get(itemId)
  if (row !== undefined) {
    row.refusal = message
  }
}

function rowRefusalText(itemId: string): string | null {
  const message = rowFor(itemId).refusal
  return message === null ? null : refusalMessageText(t, message)
}

function typePrice(itemId: string, typed: string): void {
  const row = rows.value.get(itemId)
  if (row !== undefined) {
    row.priceText = typed
  }
}

function priceIsUnreadable(itemId: string): boolean {
  const typed = rowFor(itemId).priceText
  return typed.trim().length > 0 && parseEuroInput(typed) === null
}

function priceTheLaptopCanTake(itemId: string, row: ItemRow): number | null {
  const priceCents = parseEuroInput(row.priceText)
  if (priceCents === null) {
    refuse(itemId, 'admin.itemPriceOutOfRange')
    return null
  }
  if (row.stationIds.length === 0) {
    refuse(itemId, 'admin.festival.itemNeedsAStation', { item: itemNameFor(itemId) })
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
  row.refusal = null
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
    row.refusal = null
    const placed = await items.putAtTheFestival(props.festivalId, itemId, {
      priceCents,
      stationIds: [...onItsWay.stationIds],
    })
    row.laptopAnswers += 1
    switch (placed.kind) {
      case 'ok':
        row.sentPriceText = onItsWay.priceText
        row.sentStationIds = [...onItsWay.stationIds]
        row.onItsWay = null
        row.refusal = null
        break
      case 'failed':
        row.refusal = placed.message
        row.priceText = row.sentPriceText
        row.stationIds = [...row.sentStationIds]
        row.onItsWay = null
        return
      default:
        assertNever(placed)
    }
  }
}

async function changeStations(itemId: string, stationIds: string[]): Promise<void> {
  const row = rows.value.get(itemId)
  if (row === undefined) {
    return
  }
  if (priceIsUnreadable(itemId)) {
    refuse(itemId, 'admin.itemPriceOutOfRange')
    return
  }
  if (stationIds.length === 0) {
    refuse(itemId, 'admin.festival.itemNeedsAStation', { item: itemNameFor(itemId) })
    return
  }
  row.stationIds = [...stationIds]
  await save(itemId)
}

async function setSoldOut(item: AdminItem, isSoldOut: boolean): Promise<void> {
  const started = rows.value.get(item.itemId)
  if (started !== undefined) {
    started.refusal = null
  }
  const accepted = await items.setAvailability(props.festivalId, item.itemId, !isSoldOut)
  const row = rows.value.get(item.itemId)
  if (row !== undefined) {
    row.laptopAnswers += 1
  }
  showRowRefusal(item.itemId, accepted)
}

function startPlacing(item: AdminItem): void {
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

function startCreating(): void {
  createRefusal.value = null
  isCreating.value = true
}

function stopCreating(): void {
  createRefusal.value = null
  isCreating.value = false
}

async function create(draft: AdminItemDraft): Promise<void> {
  createRefusal.value = null
  const created = await items.create(draft)
  switch (created.kind) {
    case 'ok': {
      isCreating.value = false
      const item = items.items.find((listed) => listed.itemId === created.value)
      if (item !== undefined) {
        startPlacing(item)
      }
      return
    }
    case 'failed':
      createRefusal.value = created.message
      return
    default:
      assertNever(created)
  }
}

async function remove(): Promise<void> {
  const item = removedItem.value
  removedItem.value = null
  if (item === null) {
    return
  }
  const row = rows.value.get(item.itemId)
  if (row !== undefined) {
    row.refusal = null
  }
  const accepted = await items.removeFromTheFestival(props.festivalId, item.itemId)
  showRowRefusal(item.itemId, accepted)
}
</script>

<template>
  <section class="festival-items mb-4">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3">{{ t('admin.items.title') }}</h2>

        <v-alert
          v-if="items.loadFailed || categories.loadFailed"
          class="error mb-3"
          type="error"
          variant="tonal"
        >
          {{ t('admin.loadFailed') }}
        </v-alert>

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
                  :model-value="rowFor(item.itemId).priceText"
                  :label="t('admin.items.price')"
                  :error="priceIsUnreadable(item.itemId)"
                  :error-messages="
                    priceIsUnreadable(item.itemId) ? [t('admin.items.priceInvalid')] : []
                  "
                  @update:model-value="(typed: string) => typePrice(item.itemId, typed)"
                  @blur="save(item.itemId)"
                  @keyup.enter="save(item.itemId)"
                />
                <StationSelect
                  :stations="festivalStations"
                  :selected-station-ids="rowFor(item.itemId).stationIds"
                  :error-text="null"
                  @select="(stationIds: string[]) => changeStations(item.itemId, stationIds)"
                />
                <v-spacer />
                <v-switch
                  :key="`${item.itemId}-${item.atTheFestival.isAvailable}-${rowFor(item.itemId).laptopAnswers}`"
                  class="sold-out-switch flex-grow-0"
                  density="compact"
                  color="primary"
                  hide-details
                  :model-value="!item.atTheFestival.isAvailable"
                  :label="t('admin.items.soldOut')"
                  @update:model-value="(value: boolean | null) => setSoldOut(item, value === true)"
                />
                <span class="remove-item-wrapper">
                  <v-btn
                    class="remove-item"
                    variant="text"
                    :disabled="isRunning"
                    @click="removedItem = item"
                  >
                    {{ t('admin.festival.remove') }}
                  </v-btn>
                  <v-tooltip activator="parent" location="top" :disabled="!isRunning">
                    {{ t('admin.itemStaysOnTheMenuWhileTheFestivalRuns') }}
                  </v-tooltip>
                </span>
              </div>
              <v-alert
                v-if="rowRefusalText(item.itemId) !== null"
                class="refusal mb-2"
                type="warning"
                variant="tonal"
              >
                {{ rowRefusalText(item.itemId) }}
              </v-alert>
            </div>
          </section>

          <div class="add-item-line d-flex align-center flex-wrap ga-3 mt-6">
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
            v-if="createRefusalText !== null && !isCreating"
            class="refusal mt-3"
            type="warning"
            variant="tonal"
          >
            {{ createRefusalText }}
          </v-alert>
        </template>
      </div>
    </v-card>

    <ItemDialog
      v-if="isCreating"
      :item="null"
      :error-text="createRefusalText"
      @save="create"
      @cancel="stopCreating"
    />

    <FestivalPlacementDialog
      v-if="itemToPlace !== null"
      :festival-id="festivalId"
      :item="itemToPlace"
      :stations="festivalStations"
      @placed="stopPlacing"
      @cancel="stopPlacing"
    />

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

.festival-item-row .station-select-field {
  flex: 0 1 auto;
}

.remove-item-wrapper {
  display: inline-flex;
}
</style>
