<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '../../stores/session'
import { replace } from '../../router'

const props = defineProps<{ code: string }>()
const { t } = useI18n()
const session = useSessionStore()

const failureMessage = computed(() => {
  const key = session.redeemErrorKey
  return key === null ? null : t(key)
})

onMounted(async () => {
  const redeemed = await session.redeem({ code: props.code })
  if (redeemed) {
    replace('/')
  }
})
</script>

<template>
  <v-container v-if="failureMessage !== null" class="redeem-failure">
    <v-alert class="failure-notice" type="info" variant="tonal">
      {{ failureMessage }}
    </v-alert>
    <v-btn
      v-if="session.isEnrolled"
      class="carry-on mt-4"
      color="primary"
      block
      @click="replace('/')"
    >
      {{ t('enrol.carryOn') }}
    </v-btn>
  </v-container>
  <v-container v-else class="enrolment-working" />
</template>
