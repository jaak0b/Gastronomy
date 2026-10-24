import { computed, watch, type ComputedRef } from 'vue'
import type { AdminFestivalView } from '../../shared/api/generatedSchemas'
import { navigate } from '../../shared/router/router'
import { useAdminFestivalsStore } from '../stores/festivals'

export interface OpenedFestival {
  festival: ComputedRef<AdminFestivalView | null>
  loadTheFestivalOrLeave: () => Promise<AdminFestivalView | null>
}

export function useOpenedFestival(festivalId: () => string): OpenedFestival {
  const festivals = useAdminFestivalsStore()
  const festival = computed(() => festivals.findFestivalWithId(festivalId()))

  function untilTheFestivalsAreKnown(): Promise<void> {
    return new Promise((resolve) => {
      const stopWatching = watch(
        () => festivals.hasLoaded || festivals.loadFailed,
        (areKnown) => {
          if (areKnown) {
            stopWatching()
            resolve()
          }
        },
      )
    })
  }

  async function loadTheFestivalOrLeave(): Promise<AdminFestivalView | null> {
    await festivals.load()
    if (!festivals.hasLoaded && !festivals.loadFailed) {
      await untilTheFestivalsAreKnown()
    }
    if (festival.value === null && !festivals.loadFailed) {
      navigate('/admin/overview')
    }
    return festival.value
  }

  return { festival, loadTheFestivalOrLeave }
}
