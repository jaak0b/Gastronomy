export interface ListLoadState {
  hasLoaded: boolean
  loadFailed: boolean
}

export const NOTHING_TO_LOAD: ListLoadState = { hasLoaded: true, loadFailed: false }

export function showsTheEmptyListText(load: ListLoadState, entryCount: number): boolean {
  return load.hasLoaded && !load.loadFailed && entryCount === 0
}
