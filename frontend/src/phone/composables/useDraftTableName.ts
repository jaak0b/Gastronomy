import { computed } from 'vue'
import { shownNameOf } from '../core/draftCart'
import { useOrderStore } from '../stores/order'
import { useTablePlanStore } from '../stores/tablePlan'

export function useDraftTableName() {
  const order = useOrderStore()
  const tablePlan = useTablePlanStore()

  const draftTableName = computed(() =>
    order.draft.table === null ? null : shownNameOf(order.draft.table, tablePlan.areas),
  )

  return { draftTableName }
}
