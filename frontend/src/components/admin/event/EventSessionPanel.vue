<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminEventSessionStore } from '../../../stores/admin/eventSession'

const { t } = useI18n()
const eventSession = useAdminEventSessionStore()
const name = ref('')
const confirmedName = ref('')

const requiresConfirmedName = computed(
  () => eventSession.current?.requiresConfirmedName ?? false,
)

const canStart = computed(() => {
  if (name.value.trim().length === 0) {
    return false
  }
  if (eventSession.blockingConditions.length > 0) {
    return false
  }
  return !requiresConfirmedName.value || confirmedName.value.trim() === name.value.trim()
})

async function start(isPractice: boolean): Promise<void> {
  await eventSession.start(
    name.value.trim(),
    isPractice,
    requiresConfirmedName.value ? confirmedName.value.trim() : null,
  )
}

onMounted(eventSession.load)
</script>

<template>
  <section class="admin-event">
    <h1>{{ t('admin.event.title') }}</h1>
    <p v-if="eventSession.current !== null" class="current">
      {{
        t('admin.event.current', {
          name: eventSession.current.name,
          time: eventSession.current.startedAtUtc,
        })
      }}
    </p>
    <p v-if="eventSession.current?.isPractice === true" class="practice-running">
      {{ t('admin.event.practiceRunning') }}
    </p>
    <p
      v-for="(condition, index) in eventSession.blockingConditions"
      :key="index"
      class="blocking"
    >
      {{
        t(
          condition.messageKey,
          condition.parameters,
          Number(condition.parameters.count ?? 1),
        )
      }}
    </p>
    <label>
      <span>{{ t('admin.event.title') }}</span>
      <input v-model="name" type="text" />
    </label>
    <template v-if="requiresConfirmedName">
      <p class="confirm-name">{{ t('admin.event.confirmName') }}</p>
      <input v-model="confirmedName" type="text" />
    </template>
    <p class="start-effect">{{ t('admin.event.startEffect') }}</p>
    <button type="button" class="start" :disabled="!canStart" @click="start(false)">
      {{ t('admin.event.startNew') }}
    </button>
    <button type="button" class="practice" :disabled="name.trim().length === 0" @click="start(true)">
      {{ t('admin.event.practice') }}
    </button>
    <p class="help">{{ t('admin.event.practiceHelp') }}</p>
  </section>
</template>
