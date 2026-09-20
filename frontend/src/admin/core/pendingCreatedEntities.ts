export interface PendingCreatedEntities<TItem> {
  remember(created: TItem): void
  mergeInto(shown: TItem[]): TItem[]
  clear(): void
}

export function createPendingCreatedEntities<TItem>(
  keyOf: (item: TItem) => string,
): PendingCreatedEntities<TItem> {
  const pending = new Map<string, TItem>()

  return {
    remember(created: TItem): void {
      pending.set(keyOf(created), created)
    },
    mergeInto(shown: TItem[]): TItem[] {
      const shownKeys = new Set(shown.map((item) => keyOf(item)))
      for (const key of [...pending.keys()]) {
        if (shownKeys.has(key)) {
          pending.delete(key)
        }
      }
      const missing = [...pending.values()]
      if (missing.length === 0) {
        return shown
      }
      return [...shown, ...missing]
    },
    clear(): void {
      pending.clear()
    },
  }
}
