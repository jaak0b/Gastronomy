<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import NameField from './NameField.vue'
import { useSessionStore } from '../../stores/session'
import { navigate } from '../../router'

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
  <section v-if="isReady" class="enrolment-done">
    <p class="success">{{ t('enrol.success') }}</p>
    <button type="button" class="to-catalog" @click="navigate('/')">
      {{ t('enrol.continue') }}
    </button>
  </section>
  <section v-else class="enrolment">
    <h1>{{ t('enrol.title') }}</h1>
    <p>{{ t('enrol.intro') }}</p>
    <p v-if="draftIsHeld" class="order-held">{{ t('enrol.orderHeld') }}</p>
    <NameField v-model="name" />
    <button type="button" :disabled="!canContinue" @click="submit">
      {{ t('enrol.continue') }}
    </button>
    <p v-if="session.redeemErrorKey !== null" class="error">{{ t(session.redeemErrorKey) }}</p>
  </section>
</template>
