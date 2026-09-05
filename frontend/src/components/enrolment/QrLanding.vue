<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import NameField from './NameField.vue'
import { useSessionStore } from '../../stores/session'
import { replace } from '../../router'
import LanguageSwitch from '../LanguageSwitch.vue'

const props = defineProps<{ code: string }>()
const { t } = useI18n()
const session = useSessionStore()
const name = ref('')
const draftIsHeld = ref(session.heldDraftExists())
const asksForAName = ref(false)
const codeIsSpent = ref(false)

const canContinue = computed(() => name.value.trim().length > 0)

async function submit(): Promise<void> {
  const redeemed = await session.redeem({ code: props.code, name: name.value.trim() })
  if (redeemed) {
    replace('/')
  }
}

onMounted(async () => {
  const redeemed = await session.redeem({ code: props.code })
  if (redeemed) {
    replace('/')
    return
  }

  if (session.redeemErrorKey === 'enrolment.nameMissing') {
    session.redeemErrorKey = null
    asksForAName.value = true
    return
  }

  session.redeemErrorKey = null
  codeIsSpent.value = true
})
</script>

<template>
  <v-container v-if="codeIsSpent" class="code-spent">
    <v-alert class="spent-notice" type="info" variant="tonal">
      {{ session.isEnrolled ? t('enrol.codeSpentWithSession') : t('enrol.codeSpent') }}
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
  <v-container v-else-if="asksForAName" class="enrolment">
    <LanguageSwitch
      :language="session.language"
      label-key="settings.language"
      @select="session.setLanguage"
    />
    <h1 class="text-h4 mt-4">{{ t('enrol.title') }}</h1>
    <p class="mt-2">{{ t('enrol.intro') }}</p>
    <v-alert v-if="draftIsHeld" class="order-held mt-2" type="info" variant="tonal">
      {{ t('enrol.orderHeld') }}
    </v-alert>
    <NameField v-model="name" class="mt-4" />
    <v-btn class="continue" color="primary" block :disabled="!canContinue" @click="submit">
      {{ t('enrol.continue') }}
    </v-btn>
    <v-alert v-if="session.redeemErrorKey !== null" class="error mt-4" type="error" variant="tonal">
      {{ t(session.redeemErrorKey) }}
    </v-alert>
  </v-container>
  <v-container v-else class="enrolment-working" />
</template>
