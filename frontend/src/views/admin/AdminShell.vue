<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { currentRoute, navigate, type AdminSection } from '../../router'
import { assertNever } from '../../core/assertNever'
import { request } from '../../api/client'
import AdminOverview from '../../components/admin/overview/AdminOverview.vue'
import LocationsList from '../../components/admin/locations/LocationsList.vue'
import ItemsList from '../../components/admin/items/ItemsList.vue'
import PrintersList from '../../components/admin/printers/PrintersList.vue'
import PeopleList from '../../components/admin/people/PeopleList.vue'
import NotOnLaptop from '../../components/admin/NotOnLaptop.vue'
import { bindLocaleToLaptop } from '../../appLanguageBinding'

const { t } = useI18n()
const isReachable = ref(true)

bindLocaleToLaptop()

const section = computed<AdminSection>(() => {
  const route = currentRoute.value
  return route.name === 'admin' ? route.section : 'overview'
})

const sections: AdminSection[] = [
  'overview',
  'locations',
  'items',
  'printers',
  'people',
]

function titleFor(value: AdminSection): string {
  switch (value) {
    case 'overview':
      return t('admin.overview.title')
    case 'locations':
      return t('admin.locations.title')
    case 'items':
      return t('admin.items.title')
    case 'printers':
      return t('admin.printers.title')
    case 'people':
      return t('admin.people.title')
    default:
      return assertNever(value)
  }
}

void request('/api/admin/locations').then((result) => {
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
    <LocationsList v-else-if="section === 'locations'" />
    <ItemsList v-else-if="section === 'items'" />
    <PrintersList v-else-if="section === 'printers'" />
    <PeopleList v-else />
  </div>
</template>
