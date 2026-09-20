<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ADMIN_SECTIONS, currentRoute, navigate, type AdminSection } from '../../shared/router/router'
import { assertNever } from '../../shared/core/assertNever'
import { requestAction } from '../../shared/api/client'
import AdminOverview from '../components/overview/AdminOverview.vue'
import FestivalsList from '../components/festivals/FestivalsList.vue'
import FestivalPage from '../components/festivals/FestivalPage.vue'
import StationsList from '../components/stations/StationsList.vue'
import ItemsList from '../components/items/ItemsList.vue'
import StaffList from '../components/staff/StaffList.vue'
import NotOnLaptop from '../components/NotOnLaptop.vue'
import { useLocaleBinding } from '../../shared/composables/useLocaleBinding'
import { useAppLanguageStore } from '../stores/appLanguage'

const { t } = useI18n()
const appLanguage = useAppLanguageStore()
const isReachable = ref(true)

useLocaleBinding(() => appLanguage.language)
void appLanguage.load()

const section = computed<AdminSection>(() => {
  const route = currentRoute.value
  return route.name === 'admin' ? route.section : 'festivals'
})

const festivalId = computed<string | null>(() => {
  const route = currentRoute.value
  return route.name === 'admin' ? route.festivalId : null
})

const sections: AdminSection[] = ADMIN_SECTIONS

function titleFor(value: AdminSection): string {
  switch (value) {
    case 'festivals':
      return t('admin.festivals.title')
    case 'overview':
      return t('admin.overview.title')
    case 'stations':
      return t('admin.stations.title')
    case 'items':
      return t('admin.items.title')
    case 'staff':
      return t('admin.staff.title')
    default:
      return assertNever(value)
  }
}

void requestAction('/api/admin/festivals').then((result) => {
  isReachable.value = !(result.kind === 'error' && result.status === 404)
})
</script>

<template>
  <NotOnLaptop v-if="!isReachable" />
  <div v-else class="admin-shell">
    <v-toolbar class="admin-nav" density="comfortable" color="surface">
      <v-tabs :model-value="section" class="admin-tabs">
        <v-tab
          v-for="value in sections"
          :key="value"
          :value="value"
          :class="{ 'is-selected': value === section }"
          @click="navigate(`/admin/${value}`)"
        >
          {{ titleFor(value) }}
        </v-tab>
      </v-tabs>
    </v-toolbar>
    <FestivalPage
      v-if="section === 'festivals' && festivalId !== null"
      :key="festivalId"
      :festival-id="festivalId"
    />
    <FestivalsList v-else-if="section === 'festivals'" />
    <AdminOverview v-else-if="section === 'overview'" />
    <StationsList v-else-if="section === 'stations'" />
    <ItemsList v-else-if="section === 'items'" />
    <StaffList v-else-if="section === 'staff'" />
  </div>
</template>
