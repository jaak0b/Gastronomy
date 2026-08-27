<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import NameField from './NameField.vue'
import { useSessionStore } from '../../stores/session'
import { navigate } from '../../router'
import LanguageSwitch from '../LanguageSwitch.vue'

const props = withDefaults(defineProps<{ showLanguageSwitch?: boolean }>(), {
  showLanguageSwitch: true,
})
const { t } = useI18n()
const session = useSessionStore()
const name = ref('')
const sixDigitCode = ref('')
const draftIsHeld = ref(session.heldDraftExists)
const isReady = ref(false)

const canContinue = computed(
  () => name.value.trim().length > 0 && /^\d{6}$/.test(sixDigitCode.value),
)

function onCodeInput(event: Event): void {
  sixDigitCode.value = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(0, 6)
}

async function submit(): Promise<void> {
  const redeemed = await session.redeem({
    sixDigitCode: sixDigitCode.value,
    name: name.value.trim(),
  })
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
      v-if="props.showLanguageSwitch"
      :language="session.language"
      label-key="settings.language"
      @select="session.setLanguage"
    />
    <h1 class="text-h4 mt-4">{{ t('enrolCode.title') }}</h1>
    <p class="mt-2">{{ t('enrolCode.intro') }}</p>
    <p class="text-medium-emphasis">{{ t('enrolCode.validity') }}</p>
    <v-alert
      v-if="draftIsHeld && props.showLanguageSwitch"
      class="order-held mt-2"
      type="info"
      variant="tonal"
    >
      {{ t('enrol.orderHeld') }}
    </v-alert>
    <v-text-field
      class="code-field mt-4"
      :label="t('enrolCode.field')"
      inputmode="numeric"
      :model-value="sixDigitCode"
      @input="onCodeInput"
    />
    <NameField v-model="name" />
    <v-btn class="continue" color="primary" block :disabled="!canContinue" @click="submit">
      {{ t('enrolCode.continue') }}
    </v-btn>
    <v-alert v-if="session.redeemErrorKey !== null" class="error mt-4" type="error" variant="tonal">
      {{ t(session.redeemErrorKey) }}
    </v-alert>
  </v-container>
</template>
