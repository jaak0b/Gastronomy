import type { ListLoadState } from '../../shared/core/emptyList'

export interface FestivalScopedLoad {
  loadedFestivalId: string | null
  loadFailed: boolean
}

export function loadStateAtTheFestival(list: FestivalScopedLoad, festivalId: string): ListLoadState {
  return { hasLoaded: list.loadedFestivalId === festivalId, loadFailed: list.loadFailed }
}
