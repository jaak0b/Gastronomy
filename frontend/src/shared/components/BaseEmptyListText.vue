<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <p class="base-empty-list-text text-medium-emphasis" data-test="empty-list-text">{{ t('common.labels.noEntries') }}</p>
</template>
