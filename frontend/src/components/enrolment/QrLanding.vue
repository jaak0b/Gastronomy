<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import NameField from './NameField.vue'
import { useSessionStore } from '../../stores/session'
import { navigate } from '../../router'
import LanguageSwitch from '../LanguageSwitch.vue'

const props = defineProps<{ code: string }>()
const { t } = useI18n()
const session = useSessionStore()
const name = ref('')
const draftIsHeld = ref(session.heldDraftExists)
const isReady = ref(false)

const canContinue = computed(() => name.value.trim().length > 0)

async function submit(): Promise<void> {
  const redeemed = await session.redeem({ code: props.code, name: name.value.trim() })
  if (redeemed) {
    isReady.value = true
  }
}
</script>

<template>
  <v-container v-if="isReady" class="enrolment-done">
    <v-alert class="success" type="success" variant="tonal">{{ t('enrol.success') }}</v-alert>
    <v-btn class="to-catalog mt-4" color="primary" block @click="navigate('/')">
      {{ t('enrol.continue') }}
    </v-btn>
  </v-container>
  <v-container v-else class="enrolment">
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
</template>
