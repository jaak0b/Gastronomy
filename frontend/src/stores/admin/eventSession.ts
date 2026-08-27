import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../../api/client'

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

  async function load(): Promise<void> {
    const result = await request<EventSessionState>('/api/admin/event-session')
    if (result.kind === 'ok') {
      current.value = result.data
      blockingConditions.value = result.data.blockingConditions
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

  return { current, blockingConditions, load, start }
})
