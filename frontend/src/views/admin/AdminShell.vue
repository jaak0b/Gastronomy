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
import EventSessionPanel from '../../components/admin/event/EventSessionPanel.vue'
import NotOnLaptop from '../../components/admin/NotOnLaptop.vue'
import LanguageSwitch from '../../components/LanguageSwitch.vue'
import { useSessionStore } from '../../stores/session'
import { bindLocaleToSession } from '../../localeBinding'

const { t } = useI18n()
const session = useSessionStore()
const isReachable = ref(true)

bindLocaleToSession()

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
  'event',
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
    case 'event':
      return t('admin.event.title')
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
    <nav class="admin-nav">
      <div class="admin-tabs">
        <button
          v-for="value in sections"
          :key="value"
          type="button"
          :class="{ 'is-selected': value === section }"
          @click="navigate(`/admin/${value}`)"
        >
          {{ titleFor(value) }}
        </button>
      </div>
      <LanguageSwitch
        :language="session.language"
        label-key="settings.language"
        @select="session.setLanguage"
      />
    </nav>
    <AdminOverview v-if="section === 'overview'" />
    <LocationsList v-else-if="section === 'locations'" />
    <ItemsList v-else-if="section === 'items'" />
    <PrintersList v-else-if="section === 'printers'" />
    <PeopleList v-else-if="section === 'people'" />
    <EventSessionPanel v-else />
  </div>
</template>
