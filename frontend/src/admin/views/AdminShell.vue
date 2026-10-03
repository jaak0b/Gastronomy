<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ADMIN_SECTIONS, currentRoute, navigate, type AdminSection } from '../../shared/router/router'
import { assertNever } from '../../shared/core/assertNever'
import AdminOverview from '../components/overview/AdminOverview.vue'
import FestivalsList from '../components/festivals/FestivalsList.vue'
import FestivalPage from '../components/festivals/FestivalPage.vue'
import StationsList from '../components/stations/StationsList.vue'
import ItemsList from '../components/items/ItemsList.vue'
import StaffList from '../components/staff/StaffList.vue'
import NotOnLaptop from '../components/NotOnLaptop.vue'
import { useLocaleBinding } from '../../shared/composables/useLocaleBinding'
import { useAppLanguageStore } from '../stores/appLanguage'
import { useLaptopProbe } from '../composables/useLaptopProbe'

const { t } = useI18n()
const appLanguage = useAppLanguageStore()
const { isThisTheLaptop } = useLaptopProbe()
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

void isThisTheLaptop().then((isLaptop) => {
  isReachable.value = isLaptop
})
</script>

<template>
  <NotOnLaptop v-if="!isReachable" />
  <div v-else class="admin-shell">
    <v-toolbar class="admin-nav" density="comfortable" color="surface">
      <v-tabs :model-value="section" class="admin-tabs" data-test="admin-tabs">
        <v-tab
          v-for="value in sections"
          :key="value"
          :value="value"
          :data-selected="value === section"
          data-test="admin-tab"
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
