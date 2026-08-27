<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminEventSessionStore } from '../../../stores/admin/eventSession'

const { t } = useI18n()
const eventSession = useAdminEventSessionStore()
const name = ref('')
const confirmedName = ref('')

const requiresConfirmedName = computed(() => eventSession.current?.requiresConfirmedName ?? false)

const hasName = computed(() => name.value.trim().length > 0)

const canStart = computed(() => {
  if (!hasName.value) {
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
    <p v-if="eventSession.loadFailed" class="error">{{ t('admin.loadFailed') }}</p>
    <template v-if="eventSession.hasSession && eventSession.current !== null">
      <p class="current">
        {{
          t('admin.event.current', {
            name: eventSession.current.name,
            time: eventSession.current.startedAtUtc,
          })
        }}
      </p>
      <p v-if="eventSession.current.isPractice" class="practice-running">
        {{ t('admin.event.practiceRunning') }}
      </p>
    </template>
    <p v-else class="no-session">{{ t('admin.event.none') }}</p>
    <label class="event-name">
      <span>{{ t('admin.event.name') }}</span>
      <input v-model="name" type="text" />
    </label>
    <template v-if="requiresConfirmedName">
      <p class="confirm-name">{{ t('admin.event.confirmName') }}</p>
      <label class="confirm-name-field">
        <span>{{ t('admin.event.name') }}</span>
        <input v-model="confirmedName" type="text" />
      </label>
    </template>
    <p class="start-effect">{{ t('admin.event.startEffect') }}</p>
    <div class="event-actions">
      <button type="button" class="start" :disabled="!canStart" @click="start(false)">
        {{ t('admin.event.startNew') }}
      </button>
      <div class="practice-action">
        <button type="button" class="practice" :disabled="!hasName" @click="start(true)">
          {{ t('admin.event.practice') }}
        </button>
        <p class="practice-help">{{ t('admin.event.practiceHelp') }}</p>
      </div>
    </div>
    <p v-for="(condition, index) in eventSession.blockingConditions" :key="index" class="blocking">
      {{
        condition.count === null
          ? t(condition.key, condition.parameters)
          : t(condition.key, condition.parameters, condition.count)
      }}
    </p>
  </section>
</template>
