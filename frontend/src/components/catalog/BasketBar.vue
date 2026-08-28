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
  <v-sheet class="basket-bar d-flex align-center py-3" color="background">
    <span class="summary text-body-1">{{ summary }}</span>
    <v-spacer />
    <v-btn class="to-review" color="primary" :disabled="itemCount === 0" @click="$emit('review')">
      {{ t('catalog.toReview') }}
    </v-btn>
  </v-sheet>
</template>

<style scoped>
.basket-bar {
  position: sticky;
  bottom: 0;
  z-index: 2;
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
