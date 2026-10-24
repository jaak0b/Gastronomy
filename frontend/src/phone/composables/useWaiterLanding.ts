import { computed } from 'vue'
import { waiterLandingFor } from '../../shared/core/landing'
import { useCatalogStore } from '../stores/catalog'
import { useOrderStore } from '../stores/order'
import { useTablePlanStore } from '../stores/tablePlan'

export function useWaiterLanding() {
  const catalog = useCatalogStore()
  const order = useOrderStore()
  const tablePlan = useTablePlanStore()

  const landing = computed(() =>
    waiterLandingFor({
      orderIsLocked: order.changesAreRefused,
      festivalIsRunning: catalog.catalog.festival !== null,
      areaIds: tablePlan.areas.map((area) => area.areaId),
      openedAreaId: tablePlan.openedAreaId,
    }),
  )

  return { landing }
}
