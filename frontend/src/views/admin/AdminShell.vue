<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ADMIN_SECTIONS, currentRoute, navigate, type AdminSection } from '../../router'
import { assertNever } from '../../core/assertNever'
import { request } from '../../api/client'
import AdminOverview from '../../components/admin/overview/AdminOverview.vue'
import StationsList from '../../components/admin/stations/StationsList.vue'
import ItemsList from '../../components/admin/items/ItemsList.vue'
import StaffList from '../../components/admin/staff/StaffList.vue'
import NotOnLaptop from '../../components/admin/NotOnLaptop.vue'
import { bindLocaleToLaptop } from '../../appLanguageBinding'

const { t } = useI18n()
const isReachable = ref(true)

bindLocaleToLaptop()

const section = computed<AdminSection>(() => {
  const route = currentRoute.value
  return route.name === 'admin' ? route.section : 'overview'
})

const sections: AdminSection[] = ADMIN_SECTIONS

function titleFor(value: AdminSection): string {
  switch (value) {
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

void request('/api/admin/stations').then((result) => {
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
    <AdminOverview v-if="section === 'overview'" />
    <StationsList v-else-if="section === 'stations'" />
    <ItemsList v-else-if="section === 'items'" />
    <StaffList v-else />
  </div>
</template>
