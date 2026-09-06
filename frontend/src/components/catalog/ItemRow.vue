<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage, CatalogItem } from '../../core/apiTypes'
import { itemState } from '../../core/catalogItemState'
import { assertNever } from '../../core/assertNever'
import { formatPrice } from '../../core/totals'
import { groupPositions, type ItemPosition, type PositionGroup } from '../../core/itemPositions'

const props = defineProps<{
  item: CatalogItem
  positions: ItemPosition[]
  language: AppLanguage
  readyInMinutes: number | null
}>()
const emit = defineEmits<{
  add: []
  addWithANote: [note: string]
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

const readyText = computed(() => {
  const minutes = props.readyInMinutes
  if (minutes === null) {
    return null
  }
  return minutes === 0 ? t('catalog.readyNow') : t('catalog.readyIn', { count: minutes }, minutes)
})

const groups = computed(() => groupPositions(props.positions))

const plainGroup = computed(
  () => groups.value.find((group) => group.note === null && group.stationName === null) ?? null,
)

const noteGroups = computed(() => groups.value.filter((group) => group !== plainGroup.value))

const canConfirm = computed(() => typedNote.value.trim().length > 0)

function labelFor(group: PositionGroup): string {
  return group.note ?? t('line.station', { name: group.stationName })
}

function askForANote(): void {
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
        height="64"
        :disabled="isSoldOut"
        @click="emit('add')"
      >
        <span class="name text-body-1">{{ item.name }}</span>
        <span v-if="isSoldOut" class="sold-out text-caption">{{ t('catalog.soldOut') }}</span>
        <span v-else-if="readyText !== null" class="ready-in text-caption">{{ readyText }}</span>
        <span class="price text-body-1">{{ price }}</span>
      </v-btn>
      <v-btn class="add-note" variant="text" :disabled="isSoldOut" @click="askForANote">
        {{ t('catalog.addNote') }}
      </v-btn>
    </div>

    <div
      v-for="group in noteGroups"
      :key="labelFor(group)"
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
      <button class="group-label text-body-2 text-start flex-grow-1" @click="correctTheNoteOf(group)">
        {{ labelFor(group) }}
      </button>
      <v-btn
        v-if="group.stationName !== null"
        class="change-station"
        variant="text"
        size="small"
        @click="emit('changeStation', group.indexes)"
      >
        {{ t('line.changeStation') }}
      </v-btn>
      <v-btn
        v-if="group.note !== null"
        class="group-add"
        icon="mdi-plus"
        variant="text"
        size="small"
        :aria-label="t('catalog.addOne', { name: item.name })"
        @click="emit('addWithANote', group.note)"
      />
    </div>

    <v-dialog v-model="isAsking" max-width="480">
      <v-card class="note-dialog">
        <v-card-title class="title">{{ t('catalog.noteTitle', { name: item.name }) }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="typedNote"
            class="note-input"
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

.add :deep(.v-btn__content) {
  display: flex;
  width: 100%;
  justify-content: space-between;
  gap: 12px;
}

.count {
  min-width: 2ch;
  text-align: center;
}

.group-label {
  background: none;
  border: none;
  color: inherit;
  padding: 0;
}
</style>
