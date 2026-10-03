<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { AdminCategoryView, AdminItemView } from '../../../shared/api/generatedSchemas'
import { letteringColourOn } from '../../../shared/core/letteringColour'
import type { CategoryMoveDirection } from '../../stores/categories'

const props = defineProps<{
  category: AdminCategoryView
  items: AdminItemView[]
  isAFestivalRunning: boolean
}>()
const emit = defineEmits<{
  'rename-category': []
  'move-category': [direction: CategoryMoveDirection]
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
      <v-btn
        data-test="move-category-up"
        icon="mdi-arrow-up"
        variant="text"
        @click="emit('move-category', 'up')"
      />
      <v-btn
        data-test="move-category-down"
        icon="mdi-arrow-down"
        variant="text"
        @click="emit('move-category', 'down')"
      />
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
        <span v-if="item.isActive" class="deactivate-wrapper" data-test="deactivate-wrapper">
          <v-btn
            data-test="deactivate-item"
            icon="mdi-delete"
            variant="text"
            color="error"
            :disabled="isOnTheRunningFestivalsMenu(item)"
            @click="emit('deactivate-item', item)"
          />
          <v-tooltip
            data-test="festival-menu-tooltip"
            activator="parent"
            location="top"
            :disabled="!isOnTheRunningFestivalsMenu(item)"
          >
            {{ t('errors.admin.items.isOnTheRunningFestivalsMenu') }}
          </v-tooltip>
        </span>
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

<style scoped>
.deactivate-wrapper {
  display: inline-flex;
}
</style>
