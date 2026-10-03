import type { z } from 'zod'
import { request } from '../../shared/api/client'
import type { LatestRequestGate } from '../../shared/core/latestRequestGate'

export interface AdminListLoad<TResponse, TItem> {
  path: string
  schema: z.ZodType<TResponse>
  gate: LatestRequestGate
  itemsOf: (response: TResponse) => TItem[]
  showItems: (items: TItem[]) => void
  setLoadFailed: (failed: boolean) => void
}

export async function loadAdminList<TResponse, TItem>(
  load: AdminListLoad<TResponse, TItem>,
): Promise<void> {
  load.setLoadFailed(false)
  const token = load.gate.startRequest()
  const result = await request(load.path, { schema: load.schema })
  if (!load.gate.isNewestRequest(token)) {
    return
  }
  if (result.kind !== 'ok') {
    load.setLoadFailed(true)
    return
  }
  load.showItems(load.itemsOf(result.data))
}
