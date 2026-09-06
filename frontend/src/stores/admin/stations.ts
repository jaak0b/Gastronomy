import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import { useConnectionStore } from '../connection'
import { useAdminEnrolmentStore } from './enrolment'

export interface AdminStation {
  stationId: string
  name: string
  sortOrder: number
  isActive: boolean
  hasDevice: boolean
}

export const useAdminStationsStore = defineStore('adminStations', () => {
  const stations = ref<AdminStation[]>([])
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/stations')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminStation>(result.data, 'stations')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    stations.value = rows
  }

  async function save(
    station: Pick<AdminStation, 'name' | 'sortOrder'> & { stationId?: string },
  ): Promise<boolean> {
    errorMessage.value = null
    const path =
      station.stationId === undefined
        ? '/api/admin/stations'
        : `/api/admin/stations/${station.stationId}`
    const result = await request(path, {
      method: station.stationId === undefined ? 'POST' : 'PUT',
      body: {
        name: station.name,
        sortOrder: station.sortOrder,
      },
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return false
    }
    await load()
    return true
  }

  async function setActive(id: string, isActive: boolean): Promise<void> {
    errorMessage.value = null
    const action = isActive ? 'activate' : 'deactivate'
    const result = await request(`/api/admin/stations/${id}/${action}`, { method: 'POST' })
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
      useAdminEnrolmentStore().listen(() => {
        void load()
      }),
    ]
    return () => {
      for (const release of releases) {
        release()
      }
    }
  }

  return { stations, loadFailed, errorMessage, load, save, setActive, listen }
})
