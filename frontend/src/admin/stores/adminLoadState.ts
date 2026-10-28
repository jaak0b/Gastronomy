import { defineStore } from 'pinia'
import { computed, shallowReactive, type Ref } from 'vue'
import { assertNever } from '../../shared/core/assertNever'
import { combineReleases } from '../../shared/core/combineReleases'
import {
  useConnectionStore,
  type ConnectionState,
  type ReloadEventName,
} from '../../shared/stores/connection'

function hasLostTheLaptop(state: ConnectionState): boolean {
  switch (state) {
    case 'connecting':
    case 'connected':
      return false
    case 'reconnecting':
    case 'offline':
      return true
    default:
      return assertNever(state)
  }
}

export const useAdminLoadStateStore = defineStore('adminLoadState', () => {
  const connection = useConnectionStore()
  const failureFlagsOnScreen = shallowReactive<Ref<boolean>[]>([])

  const aLoadOnScreenFailed = computed(() =>
    failureFlagsOnScreen.some((lastLoadFailed) => lastLoadFailed.value),
  )
  const showsLoadFailure = computed(
    () => aLoadOnScreenFailed.value || hasLostTheLaptop(connection.state),
  )

  function listenToTheLaptop(
    events: readonly ReloadEventName[],
    reload: () => Promise<void>,
    lastLoadFailed: Ref<boolean>,
  ): () => void {
    failureFlagsOnScreen.push(lastLoadFailed)
    return combineReleases(connection.listenToTheLaptop(events, reload), () => {
      const index = failureFlagsOnScreen.indexOf(lastLoadFailed)
      if (index !== -1) {
        failureFlagsOnScreen.splice(index, 1)
      }
    })
  }

  return { showsLoadFailure, listenToTheLaptop }
})
