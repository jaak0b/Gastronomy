<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BaseIconAction from './BaseIconAction.vue'

const { t } = useI18n()
</script>

<template>
  <BaseIconAction icon="mdi-pencil" :label="t('admin.common.actions.edit')" />
</template>
