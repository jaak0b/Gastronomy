<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useAdminLoadStateStore } from '../stores/adminLoadState'

const { t } = useI18n()
const loadState = useAdminLoadStateStore()
</script>

<template>
  <v-alert
    v-if="loadState.showsLoadFailure"
    class="load-failure-bar"
    data-test="load-failure-bar"
    type="error"
    variant="tonal"
    rounded="0"
  >
    {{ t('admin.common.errors.loadFailed') }}
  </v-alert>
</template>
