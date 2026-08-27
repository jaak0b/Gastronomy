import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { listFrom, request } from '../../api/client'

export interface BlockingCondition {
  messageKey: string
  parameters: Record<string, string | number>
}

export interface EventSessionState {
  id: string
  name: string
  isPractice: boolean
  startedAtUtc: string
  blockingConditions: BlockingCondition[]
  requiresConfirmedName: boolean
}

export const useAdminEventSessionStore = defineStore('adminEventSession', () => {
  const current = ref<EventSessionState | null>(null)
  const blockingConditions = ref<BlockingCondition[]>([])
  const loadFailed = ref(false)

  const hasSession = computed(
    () =>
      current.value !== null &&
      current.value.name.trim().length > 0 &&
      current.value.startedAtUtc !== null &&
      current.value.startedAtUtc !== undefined,
  )

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<Partial<EventSessionState>>('/api/admin/event-session')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const payload = result.data
    blockingConditions.value = listFrom<BlockingCondition>(payload, 'blockingConditions') ?? []
    if (typeof payload?.id !== 'string' || typeof payload?.name !== 'string') {
      current.value = null
      return
    }
    current.value = {
      id: payload.id,
      name: payload.name,
      isPractice: payload.isPractice === true,
      startedAtUtc: payload.startedAtUtc ?? '',
      blockingConditions: blockingConditions.value,
      requiresConfirmedName: payload.requiresConfirmedName === true,
    }
  }

  async function start(name: string, isPractice: boolean, confirmedName: string | null): Promise<boolean> {
    const result = await request<EventSessionState>('/api/admin/event-session', {
      method: 'POST',
      body: { name, isPractice, confirmedName },
    })
    if (result.kind === 'error') {
      await load()
      return false
    }
    await load()
    return result.kind === 'ok'
  }

  return { current, hasSession, blockingConditions, loadFailed, load, start }
})
