<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { AdminCategoryView, AdminItemView, type MoveDirection } from '../../../shared/api/generatedSchemas'
import { letteringColourOn } from '../../../shared/core/letteringColour'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseMoveButtons from '../BaseMoveButtons.vue'
import BaseTrashButton from '../BaseTrashButton.vue'

const props = defineProps<{
  category: AdminCategoryView
  items: AdminItemView[]
  isAFestivalRunning: boolean
}>()
const emit = defineEmits<{
  'rename-category': []
  'move-category': [direction: MoveDirection]
  'deactivate-category': []
  'activate-category': []
  'edit-ingredients': [item: AdminItemView]
  'edit-item': [item: AdminItemView]
  'deactivate-item': [item: AdminItemView]
  'reactivate-item': [item: AdminItemView]
}>()

const { t } = useI18n()

function isOnTheRunningFestivalsMenu(item: AdminItemView): boolean {
  return props.isAFestivalRunning && item.atTheFestival !== null
}
</script>

<template>
  <section class="category-section" data-test="category-section">
    <div class="category-heading d-flex align-center ga-2 py-2">
      <h2
        class="text-subtitle-1 font-weight-bold px-3 py-1 rounded"
        data-test="category-name"
        :style="{
          backgroundColor: category.colourHex,
          color: letteringColourOn(category.colourHex),
        }"
      >
        {{ category.name }}
      </h2>
      <v-chip
        v-if="!category.isActive"
        data-test="category-deactivated"
        size="small"
        color="grey"
      >
        {{ t('admin.common.labels.deactivated') }}
      </v-chip>
      <v-spacer />
      <v-btn
        data-test="rename-category"
        icon="mdi-pencil"
        variant="text"
        @click="emit('rename-category')"
      />
      <BaseMoveButtons @move="(direction) => emit('move-category', direction)" />
      <v-btn
        v-if="category.isActive"
        data-test="deactivate-category"
        icon="mdi-eye-off"
        variant="text"
        color="error"
        @click="emit('deactivate-category')"
      />
      <v-btn
        v-else
        data-test="activate-category"
        variant="text"
        @click="emit('activate-category')"
      >
        {{ t('admin.categories.actions.activate') }}
      </v-btn>
    </div>
    <v-card
      v-for="item in items"
      :key="item.itemId"
      class="mb-2"
      data-test="item-row"
      :data-test-id="item.itemId"
    >
      <div class="d-flex align-center flex-wrap ga-2 px-4 py-2" data-test="item-line">
        <span class="text-body-1" data-test="item-name">{{ item.name }}</span>
        <v-chip v-if="!item.isActive" data-test="item-deactivated" size="small" color="grey">
          {{ t('admin.common.labels.deactivated') }}
        </v-chip>
        <v-spacer />
        <v-btn data-test="edit-ingredients" variant="text" @click="emit('edit-ingredients', item)">
          {{ t('admin.ingredients.actions.edit') }}
        </v-btn>
        <v-btn data-test="edit-item" variant="text" @click="emit('edit-item', item)">
          {{ t('admin.common.actions.edit') }}
        </v-btn>
        <BaseActionWithRefusalReason
          v-if="item.isActive"
          class="deactivate-wrapper"
          data-test="deactivate-wrapper"
          :refusal-reason="isOnTheRunningFestivalsMenu(item) ? t('errors.admin.items.isOnTheRunningFestivalsMenu') : null"
        >
          <template #default="{ refused }">
            <BaseTrashButton data-test="deactivate-item" :disabled="refused" @click="emit('deactivate-item', item)" />
          </template>
        </BaseActionWithRefusalReason>
        <v-btn
          v-else
          data-test="reactivate-item"
          variant="text"
          @click="emit('reactivate-item', item)"
        >
          {{ t('admin.items.actions.activate') }}
        </v-btn>
      </div>
    </v-card>
  </section>
</template>
