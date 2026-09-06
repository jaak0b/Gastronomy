import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import type { AdminDevice } from '../../core/apiTypes'
import { useConnectionStore } from '../connection'

export const useAdminDevicesStore = defineStore('adminDevices', () => {
  const devices = ref<AdminDevice[]>([])
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/devices')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminDevice>(result.data, 'devices')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    devices.value = rows
  }

  async function revoke(deviceId: string): Promise<void> {
    errorMessage.value = null
    const result = await request(`/api/admin/devices/${deviceId}/revoke`, { method: 'POST' })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      connection.onEvent('EnrolmentCompleted', () => {
        void load()
      }),
    ]
    return () => {
      for (const release of releases) {
        release()
      }
    }
  }

  return { devices, loadFailed, errorMessage, load, revoke, listen }
})
