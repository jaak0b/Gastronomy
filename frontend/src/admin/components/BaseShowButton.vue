<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BaseIconAction from './BaseIconAction.vue'

const { t } = useI18n()
</script>

<template>
  <BaseIconAction icon="mdi-eye" :label="t('admin.festivals.actions.show')" />
</template>
