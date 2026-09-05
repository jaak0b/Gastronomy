<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { OpenTable } from '../core/apiTypes'
import { formatPrice } from '../core/totals'
import { useOpenItemsStore } from '../stores/openItems'
import { useSessionStore } from '../stores/session'
import OpenTablePanel from '../components/openItems/OpenTablePanel.vue'
import FreeOfChargeDialog from '../components/openItems/FreeOfChargeDialog.vue'
import SettleNotice from '../components/openItems/SettleNotice.vue'

const { t } = useI18n()
const openItems = useOpenItemsStore()
const session = useSessionStore()

const freeOfChargeIsOpen = ref(false)
const openedTables = ref<string[]>([])
let stopListening: (() => void) | null = null

const selectedTotal = computed(() =>
  formatPrice(openItems.selectedTotalCents, session.language),
)
const somethingIsSelected = computed(() => openItems.selectedItemIds.length > 0)
const everythingIsSettled = computed(
  () => openItems.hasLoaded && !openItems.loadFailed && openItems.tables.length === 0,
)

onMounted(async () => {
  stopListening = openItems.listen()
  await openItems.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})

async function settle(): Promise<void> {
  await openItems.settle()
}

async function settleFreeOfCharge(paymentNotice: string): Promise<void> {
  const wasAccepted = await openItems.settleFreeOfCharge(paymentNotice)
  if (wasAccepted) {
    freeOfChargeIsOpen.value = false
  }
}

function setWholeTable(table: OpenTable, isWanted: boolean): void {
  openItems.setWholeTable(table, isWanted)
}
</script>

<template>
  <v-container class="open-items">
    <h1 class="text-h5 mb-2">{{ t('openItems.title') }}</h1>
    <v-alert v-if="openItems.loadFailed" class="load-failed mb-2" type="warning" variant="tonal">
      {{ t('openItems.loadFailed') }}
    </v-alert>
    <v-alert
      v-if="openItems.itemsWithoutAnOrderCount > 0"
      class="list-incomplete mb-2"
      type="warning"
      variant="tonal"
    >
      {{
        t(
          'openItems.listIncomplete',
          { count: openItems.itemsWithoutAnOrderCount },
          openItems.itemsWithoutAnOrderCount,
        )
      }}
    </v-alert>
    <SettleNotice
      v-if="openItems.notice !== null && !freeOfChargeIsOpen"
      class="mb-2"
      :notice="openItems.notice"
      closable
      @dismiss="openItems.dismissNotice"
    />
    <v-alert v-if="everythingIsSettled" class="empty" type="info" variant="tonal">
      {{ t('openItems.empty') }}
    </v-alert>
    <v-expansion-panels v-model="openedTables" class="tables" multiple>
      <OpenTablePanel
        v-for="table in openItems.tables"
        :key="table.tableName"
        :table="table"
        :selected-item-ids="openItems.selectedItemIds"
        :language="session.language"
        @toggle-item="openItems.toggleItem"
        @set-whole-table="setWholeTable(table, $event)"
      />
    </v-expansion-panels>
    <v-sheet v-if="somethingIsSelected" class="settle-footer pt-3 pb-4" color="background">
      <p class="selected-total text-h6 mb-2">
        {{ t('openItems.selected', { amount: selectedTotal }) }}
      </p>
      <v-btn
        class="settle"
        color="primary"
        block
        size="x-large"
        :disabled="openItems.isSettling"
        @click="settle"
      >
        {{ t('openItems.settle') }}
      </v-btn>
      <v-btn
        class="settle-free-of-charge mt-2"
        color="primary"
        variant="outlined"
        block
        size="large"
        :disabled="openItems.isSettling"
        @click="freeOfChargeIsOpen = true"
      >
        {{ t('openItems.settleFreeOfCharge') }}
      </v-btn>
    </v-sheet>
    <FreeOfChargeDialog
      v-if="freeOfChargeIsOpen"
      :is-settling="openItems.isSettling"
      :notice="openItems.notice"
      @confirm="settleFreeOfCharge"
      @cancel="freeOfChargeIsOpen = false"
    />
  </v-container>
</template>

<style scoped>
.open-items {
  padding-bottom: 96px;
}

.settle-footer {
  position: sticky;
  bottom: 0;
  z-index: 2;
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
