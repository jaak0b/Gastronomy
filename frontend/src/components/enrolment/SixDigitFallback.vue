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
  <section v-if="isReady" class="enrolment-done">
    <p class="success">{{ t('enrol.success') }}</p>
    <button type="button" class="to-catalog" @click="navigate('/')">
      {{ t('enrol.continue') }}
    </button>
  </section>
  <section v-else class="enrolment">
    <LanguageSwitch
      v-if="props.showLanguageSwitch"
      :language="session.language"
      label-key="settings.language"
      @select="session.setLanguage"
    />
    <h1>{{ t('enrolCode.title') }}</h1>
    <p>{{ t('enrolCode.intro') }}</p>
    <p>{{ t('enrolCode.validity') }}</p>
    <p v-if="draftIsHeld && props.showLanguageSwitch" class="order-held">
      {{ t('enrol.orderHeld') }}
    </p>
    <label class="code-field">
      <span>{{ t('enrolCode.field') }}</span>
      <input type="text" inputmode="numeric" :value="sixDigitCode" @input="onCodeInput" />
    </label>
    <NameField v-model="name" />
    <button type="button" :disabled="!canContinue" @click="submit">
      {{ t('enrolCode.continue') }}
    </button>
    <p v-if="session.redeemErrorKey !== null" class="error">{{ t(session.redeemErrorKey) }}</p>
  </section>
</template>
