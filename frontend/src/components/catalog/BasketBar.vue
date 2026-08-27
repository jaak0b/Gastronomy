<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../core/apiTypes'
import { formatPrice } from '../../core/totals'

const props = defineProps<{ itemCount: number; totalCents: number; language: AppLanguage }>()
defineEmits<{ review: [] }>()

const { t } = useI18n()

const summary = computed(() =>
  props.itemCount === 0
    ? t('catalog.basketEmpty')
    : t(
        'catalog.basketSummary',
        { count: props.itemCount, total: formatPrice(props.totalCents, props.language) },
        props.itemCount,
      ),
)
</script>

<template>
  <div class="basket-bar">
    <span class="summary">{{ summary }}</span>
    <button type="button" :disabled="itemCount === 0" @click="$emit('review')">
      {{ t('catalog.toReview') }}
    </button>
  </div>
</template>
