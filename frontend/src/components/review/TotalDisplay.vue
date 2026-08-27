<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../core/apiTypes'
import { formatPrice } from '../../core/totals'

const props = defineProps<{ totalCents: number; language: AppLanguage }>()

const { t } = useI18n()
const total = computed(() => formatPrice(props.totalCents, props.language))
</script>

<template>
  <div class="total-display">
    <span class="label">{{ t('review.total') }}</span>
    <span class="amount">{{ total }}</span>
    <p class="help">{{ t('review.totalHelp') }}</p>
  </div>
</template>
