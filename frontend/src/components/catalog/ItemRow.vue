<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage, CatalogItem } from '../../core/apiTypes'
import { itemState } from '../../core/catalogItemState'
import { assertNever } from '../../core/assertNever'
import type { EstimateRange } from '../../core/estimates'
import { withRangeEstimate } from '../../core/estimateWording'
import { formatPrice } from '../../core/totals'
import { groupPositions, type ItemPosition, type PositionGroup } from '../../core/itemPositions'
import { needsStationChoice } from '../../core/routingPreview'

const props = defineProps<{
  item: CatalogItem
  positions: ItemPosition[]
  language: AppLanguage
  estimateRange: EstimateRange | null
}>()
const emit = defineEmits<{
  add: []
  addWithANote: [note: string]
  addWithANoteAtAStation: []
  addLikeGroup: [note: string | null, stationId: string | null]
  removeOne: [index: number]
  renameNote: [indexes: number[], note: string]
  changeStation: [indexes: number[]]
}>()

const { t } = useI18n()

const isAsking = ref(false)
const typedNote = ref('')
const groupBeingCorrected = ref<PositionGroup | null>(null)

const isSoldOut = computed(() => {
  const state = itemState(props.item)
  switch (state) {
    case 'available':
      return false
    case 'soldOut':
      return true
    default:
      return assertNever(state)
  }
})

const price = computed(() => formatPrice(props.item.priceCents, props.language))

const nameWithEstimate = computed(() =>
  withRangeEstimate(props.item.name, isSoldOut.value ? null : props.estimateRange, t),
)

const groups = computed(() => groupPositions(props.positions))

const plainGroup = computed(
  () => groups.value.find((group) => group.note === null && group.stationName === null) ?? null,
)

const noteGroups = computed(() => groups.value.filter((group) => group !== plainGroup.value))

const canConfirm = computed(() => typedNote.value.trim().length > 0)

function askForANote(): void {
  if (needsStationChoice(props.item)) {
    emit('addWithANoteAtAStation')
    return
  }
  groupBeingCorrected.value = null
  typedNote.value = ''
  isAsking.value = true
}

function correctTheNoteOf(group: PositionGroup): void {
  if (group.note === null) {
    return
  }
  groupBeingCorrected.value = group
  typedNote.value = group.note
  isAsking.value = true
}

function confirm(): void {
  const note = typedNote.value.trim()
  const corrected = groupBeingCorrected.value
  isAsking.value = false
  if (corrected === null) {
    emit('addWithANote', note)
    return
  }
  emit('renameNote', corrected.indexes, note)
}

function mostRecentOf(group: PositionGroup): number {
  return group.indexes[group.indexes.length - 1]
}
</script>

<template>
  <div class="item-row" :class="{ 'is-sold-out': isSoldOut }">
    <div class="item-line d-flex align-center">
      <v-btn
        v-if="plainGroup !== null"
        class="remove-one"
        icon="mdi-minus"
        variant="text"
        size="large"
        :aria-label="t('catalog.removeOne', { name: item.name })"
        @click="emit('removeOne', mostRecentOf(plainGroup))"
      />
      <span v-if="plainGroup !== null" class="count text-h6">{{ plainGroup.indexes.length }}</span>
      <v-btn
        class="add flex-grow-1"
        variant="text"
        :disabled="isSoldOut"
        @click="emit('add')"
      >
        <span class="name text-body-1">{{ nameWithEstimate }}</span>
        <span v-if="isSoldOut" class="sold-out text-caption">{{ t('catalog.soldOut') }}</span>
        <span class="price text-body-1">{{ price }}</span>
      </v-btn>
      <v-btn class="add-note" variant="text" :disabled="isSoldOut" @click="askForANote">
        {{ t('catalog.addNote') }}
      </v-btn>
    </div>

    <div
      v-for="group in noteGroups"
      :key="group.indexes[0]"
      class="note-group d-flex align-center ga-2 ps-6 pe-2 pb-2"
    >
      <v-btn
        class="group-remove"
        icon="mdi-minus"
        variant="text"
        size="small"
        :aria-label="t('catalog.removeOne', { name: item.name })"
        @click="emit('removeOne', mostRecentOf(group))"
      />
      <span class="group-count text-body-1">{{ group.indexes.length }}</span>
      <div class="group-label text-body-2 text-start flex-grow-1 d-flex flex-column">
        <button
          v-if="group.stationName !== null"
          class="group-station"
          @click="emit('changeStation', group.indexes)"
        >
          {{ t('line.station', { name: group.stationName }) }}
        </button>
        <button
          v-if="group.note !== null"
          class="group-note text-medium-emphasis"
          @click="correctTheNoteOf(group)"
        >
          {{ group.note }}
        </button>
      </div>
      <v-btn
        class="group-add"
        icon="mdi-plus"
        variant="text"
        size="small"
        :disabled="isSoldOut"
        :aria-label="t('catalog.addOne', { name: item.name })"
        @click="emit('addLikeGroup', group.note, group.stationId)"
      />
    </div>

    <v-dialog v-model="isAsking" max-width="480">
      <v-card class="note-dialog">
        <v-card-title class="title">{{ t('catalog.noteTitle', { name: item.name }) }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="typedNote"
            class="note-input"
            maxlength="200"
            autofocus
            :label="t('catalog.itemNote')"
            :placeholder="t('catalog.lineNotePlaceholder')"
            persistent-placeholder
            @keyup.enter="canConfirm && confirm()"
          />
        </v-card-text>
        <v-card-actions>
          <v-btn class="note-cancel" variant="text" @click="isAsking = false">
            {{ t('catalog.noteCancel') }}
          </v-btn>
          <v-spacer />
          <v-btn class="note-confirm" color="primary" variant="tonal" :disabled="!canConfirm" @click="confirm">
            {{ groupBeingCorrected === null ? t('catalog.noteAdd') : t('catalog.noteSave') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.item-row + .item-row {
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.item-line {
  min-width: 0;
}

.remove-one,
.add-note {
  align-self: stretch;
  flex: 0 0 auto;
  height: auto;
  border-radius: 8px;
}

.add {
  flex: 1 1 auto;
  min-width: 0;
  min-height: 64px;
  height: auto;
  padding-block: 0.5rem;
}

.add :deep(.v-btn__content) {
  display: flex;
  width: 100%;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  white-space: normal;
}

.name {
  flex: 1 1 auto;
  min-width: 0;
  text-align: start;
  overflow-wrap: anywhere;
}

.price,
.sold-out {
  flex: 0 0 auto;
  white-space: nowrap;
}

.count {
  min-width: 2ch;
  text-align: center;
  align-self: center;
}

.group-station,
.group-note {
  background: none;
  border: none;
  color: inherit;
  text-align: start;
  padding-block: 6px;
  min-height: 2.5rem;
}
</style>
