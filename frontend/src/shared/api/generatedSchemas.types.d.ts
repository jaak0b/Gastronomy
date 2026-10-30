
  export namespace Schemas {
    // <Schemas>
  export type AreaShapeGeometry = { x: number, y: number, width: number, height: number, rotation: number }
export type AdminTableView = { tableId: string, name: string, geometry: AreaShapeGeometry, isHidden: boolean, hasItemsToHandOutOrSettleAtTheRunningFestival: boolean }
export type AdminStationShapeView = { stationId: string, stationName: string, geometry: AreaShapeGeometry }
export type AdminAreaView = { areaId: string, name: string, isHidden: boolean, hasItemsToHandOutOrSettleAtTheRunningFestival: boolean, tables: Array<AdminTableView>, stationShapes: Array<AdminStationShapeView> }
export type AdminCategoryView = { categoryId: string, name: string, colourHex: string, sortOrder: number, isActive: boolean }
export type AdminCategoryListView = { categories: Array<AdminCategoryView> }
export type IngredientUsagePoint = { atUtc: string, usedAmount: number }
export type StockWarning = ("none" | "runsOutSoon" | "usedUp")
export type AdminFestivalIngredientView = { ingredientId: string, name: string, unitId: string, unitName: string, availableAmount: (null | number), usedAmount: number, usage: Array<IngredientUsagePoint>, runsOutAtUtcSinceStart: (null | string), runsOutAtUtcFromLastTwoHours: (null | string), stockWarning: StockWarning, runsOutSoonAtUtc: (null | string) }
export type AdminFestivalIngredientListView = { ingredients: Array<AdminFestivalIngredientView> }
export type AdminFestivalView = { festivalId: string, name: string, startsAtUtc: string, endsAtUtc: string, isHidden: boolean, isRunning: boolean, stationCount: number, menuItemCount: number, orderCount: number }
export type AdminFestivalListView = { festivals: Array<AdminFestivalView> }
export type AdminIngredientView = { ingredientId: string, name: string, unitId: string, unitName: string, usedByItemNames: Array<string> }
export type AdminIngredientListView = { ingredients: Array<AdminIngredientView> }
export type AdminItemAtFestivalView = { priceCents: number, isAvailable: boolean, stationIds: Array<string> }
export type AdminItemIngredientView = { ingredientId: string, name: string, unitId: string, unitName: string, amount: number }
export type AdminItemView = { itemId: string, name: string, categoryId: string, sortOrder: number, isActive: boolean, productionMinutes: (null | number), isQueueIndependent: boolean, atTheFestival: (null | AdminItemAtFestivalView), ingredients: Array<AdminItemIngredientView> }
export type AdminItemListView = { items: Array<AdminItemView> }
export type AdminStaffMemberView = { staffMemberId: string, name: string, isActive: boolean, hasDevice: boolean }
export type AdminStaffMemberListView = { staffMembers: Array<AdminStaffMemberView> }
export type AdminStationView = { stationId: string, name: string, sortOrder: number, isActive: boolean, hasDevice: boolean, isAtFestival: boolean, hasItemsToHandOutAtTheRunningFestival: boolean, strandedItemCountWhenSwitchedOff: (null | number), strandedItemCountWhenRemovedFromTheFestival: (null | number) }
export type AdminStationListView = { stations: Array<AdminStationView> }
export type AdminTablePlanView = { areas: Array<AdminAreaView> }
export type AdminUnitView = { unitId: string, name: string, usedByIngredientNames: Array<string> }
export type AdminUnitListView = { units: Array<AdminUnitView> }
export type CancelItemsRequest = { orderItemIds: (null | Array<string>) }
export type CancellationView = { cashToReturnCents: number }
export type CatalogCategoryView = { categoryId: string, name: string, colourHex: string, sortOrder: number }
export type CatalogItemView = { id: string, categoryId: string, name: string, priceCents: number, sortOrder: number, isAvailable: boolean, productionMinutes: (null | number), isQueueIndependent: boolean, stationIds: Array<string> }
export type CatalogStationView = { id: string, name: string, sortOrder: number }
export type RunningFestivalView = { festivalId: string, name: string }
export type CatalogView = { festival: (null | RunningFestivalView), categories: Array<CatalogCategoryView>, items: Array<CatalogItemView>, stations: Array<CatalogStationView> }
export type CopiedTablesView = { tableIds: Array<string> }
export type CopyTableRequest = { sourceTableId: string, geometry: (null | AreaShapeGeometry) }
export type CopyTablesRequest = { areaId: string, tables: (null | Array<CopyTableRequest>) }
export type CreateInvitationRequest = Partial<{ staffMemberId: (null | string), stationId: (null | string) }>
export type DeliveryMode = ("together" | "asItComes")
export type DeviceRevokedEvent = { deviceId: string }
export type EnrolmentCompletedEvent = { staffMemberId: (null | string), stationId: (null | string), ownerName: string, deviceId: string }
export type EstimateQuoteLine = { catalogItemId: string, stationId: string, units: number }
export type EstimateQuoteRequest = { lines: Array<EstimateQuoteLine> }
export type StationQuoteView = { stationId: string, readyInMinutes: (null | number) }
export type EstimateQuoteView = { stations: Array<StationQuoteView> }
export type StaffMemberView = { id: string, name: string }
export type StationSummaryView = { id: string, name: string }
export type InvitationView = { invitationId: string, qrUrl: string, expiresAtUtc: string, staffMember: (null | StaffMemberView), station: (null | StationSummaryView) }
export type ItemEstimateView = { catalogItemId: string, stationId: string, readyInMinutes: number }
export type LanguageChangeRequest = { language: (null | string) }
export type LanguageView = { language: string }
export type MoveDirection = ("up" | "down")
export type MoveAreaRequest = { direction: MoveDirection, showsHiddenAreas: boolean }
export type MoveCategoryRequest = { direction: MoveDirection }
export type MoveTableRequest = { tableId: string, geometry: (null | AreaShapeGeometry) }
export type MoveTablesRequest = { tables: (null | Array<MoveTableRequest>) }
export type OrderDeliveryModeRequest = { stationId: string, deliveryMode: DeliveryMode }
export type OrderItemState = ("open" | "handedOut" | "cancelled" | "cancellationAcknowledged")
export type OrderItemStatusChangeView = { state: OrderItemState, changedAtUtc: string, staffMemberName: (null | string), stationName: (null | string) }
export type PaymentMethod = ("none" | "cash" | "card" | null)
export type OrderItemHistoryView = { statusChanges: Array<OrderItemStatusChangeView>, settledAtUtc: (null | string), chargedPriceCents: (null | number), paymentMethod: (null | PaymentMethod), settledByStaffMemberName: (null | string), paymentNotice: (null | string) }
export type OrderItemRequest = { catalogItemId: string, unitPriceCents: number, note?: (null | string), stationId?: (null | string) }
export type OrderStatus = ("open" | "partiallyFulfilled" | "fulfilled")
export type PaymentStatus = ("notPaid" | "paid" | null)
export type StationOrderView = { stationOrderId: string, stationId: string, stationName: string, stationOrderNumber: number, deliveryMode: DeliveryMode, itemIds: Array<string> }
export type PlacedOrderView = { orderId: string, globalOrderNumber: number, status: OrderStatus, totalCents: number, createdAtUtc: string, stationOrders: Array<StationOrderView> }
export type PlaceOrderRequest = { clientOrderId: string, tableId: string, items: (null | Array<OrderItemRequest>), deliveryModes?: (null | Array<OrderDeliveryModeRequest>) }
export type RedeemedEnrolmentView = { deviceId: string, deviceToken: string, staffMember: (null | StaffMemberView), station: (null | StationSummaryView), language: string }
export type RedeemEnrolmentRequest = { code: (null | string), name?: (null | string), previousDeviceToken?: (null | string) }
export type RenameStaffMemberRequest = { name: (null | string) }
export type SaveAreaRequest = { name: (null | string) }
export type SaveCategoryRequest = { name: (null | string), colourHex: (null | string) }
export type SavedAreaView = { areaId: string }
export type SavedFestivalIngredientView = { ingredientId: string }
export type SavedFestivalView = { festivalId: string }
export type SavedIngredientView = { ingredientId: string }
export type SavedItemView = { itemId: string }
export type SavedStationView = { stationId: string }
export type SavedTableView = { tableId: string }
export type SavedUnitView = { unitId: string }
export type SaveFestivalIngredientRequest = { availableAmount: (null | number) }
export type SaveFestivalItemRequest = { priceCents: number, stationIds: (null | Array<string>) }
export type SaveFestivalRequest = { name: (null | string), startsAtUtc: string, endsAtUtc: string }
export type SaveIngredientRequest = { name: (null | string), unitId: (null | string) }
export type SaveItemIngredientRequest = { amount: (null | number) }
export type SaveItemRequest = { name: (null | string), categoryId: (null | string), sortOrder: number, productionMinutes?: (null | number), isQueueIndependent?: boolean }
export type SaveStationRequest = { name: (null | string), sortOrder: number }
export type SaveStationShapeRequest = { areaId: string, geometry: (null | AreaShapeGeometry) }
export type SaveTableRequest = { areaId: string, name: (null | string), geometry: (null | AreaShapeGeometry) }
export type SaveUnitRequest = { name: (null | string) }
export type SessionView = { deviceId: string, staffMember: (null | StaffMemberView), station: (null | StationSummaryView), language: string }
export type SetAvailabilityRequest = { isAvailable: boolean }
export type SettleLineRequest = { orderItemId: string, paidPriceCents: (null | number), paymentNotice?: (null | string) }
export type SettleItemsRequest = { lines: (null | Array<SettleLineRequest>), paymentMethod: (null | PaymentMethod) }
export type SettlementView = { settledOrderItemIds: Array<string>, reappliedOrderItemIds: Array<string>, alreadySettledByOthersOrderItemIds: Array<string> }
export type ShareState = ("none" | "some" | "all")
export type StationQueueItemView = { orderItemId: string, itemName: string, note: (null | string), state: OrderItemState, stateSetAtUtc: string }
export type StationOrderQueueView = { stationOrderId: string, globalOrderNumber: number, stationOrderNumber: number, tableName: string, staffMemberName: string, deliveryMode: DeliveryMode, createdAtUtc: string, isHiddenFromAsItComesQueue: boolean, itemCount: number, fulfilledItemCount: number, items: Array<StationQueueItemView> }
export type StationFulfilledView = { stationOrders: Array<StationOrderQueueView> }
export type StationItemSelectionRequest = { orderItemIds: (null | Array<string>) }
export type StationQueueView = { station: StationSummaryView, orders: Array<StationOrderQueueView>, asItComes: Array<StationOrderQueueView> }
export type TableOrderRecordItemView = { orderItemId: string, orderId: string, globalOrderNumber: number, itemName: string, note: (null | string), unitPriceCents: number, state: OrderItemState, stateSetAtUtc: string, wasHandedOutWhenCancelled: boolean, settledAtUtc: (null | string), chargedPriceCents: (null | number) }
export type TableOrderRecordView = { orderId: string, globalOrderNumber: number, createdAtUtc: string, staffMemberName: string, notCancelledItemCount: number, handedOutItemCount: number, items: Array<TableOrderRecordItemView> }
export type TableOrderReportView = { tableId: string, tableName: string, orders: Array<TableOrderRecordView> }
export type TableOrderSearchRequest = Partial<{ orderStatus: (null | OrderStatus), paymentStatus: (null | PaymentStatus), includeCancelled: (null | boolean) }>
export type TablePlanTableView = { tableId: string, name: string, geometry: AreaShapeGeometry, handOutProgress: ShareState, settlementProgress: ShareState }
export type TablePlanStationShapeView = { stationName: string, geometry: AreaShapeGeometry }
export type TablePlanAreaView = { areaId: string, name: string, tables: Array<TablePlanTableView>, stationShapes: Array<TablePlanStationShapeView> }
export type TablePlanView = { areas: Array<TablePlanAreaView> }

    // </Schemas>
    }
  
  export namespace Endpoints {
  // <Endpoints>
  
  export type post__api_enrolment_redeem = {
      method: "POST",
      path: "/api/enrolment/redeem",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.RedeemEnrolmentRequest,
          }
      responses: {200: Schemas.RedeemedEnrolmentView,
},
      
    }
export type get__api_catalog = {
      method: "GET",
      path: "/api/catalog",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.CatalogView,
},
      
    }
export type get__api_tablePlan = {
      method: "GET",
      path: "/api/table-plan",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.TablePlanView,
},
      
    }
export type get__api_language = {
      method: "GET",
      path: "/api/language",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.LanguageView,
},
      
    }
export type post__api_admin_enrolment_invitations = {
      method: "POST",
      path: "/api/admin/enrolment/invitations",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.CreateInvitationRequest,
          }
      responses: {201: Schemas.InvitationView,
},
      
    }
export type get__api_admin_enrolment_invitations_InvitationId_qr_svg = {
      method: "GET",
      path: "/api/admin/enrolment/invitations/{invitationId}/qr.svg",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { invitationId: string },
        
        
        
          }
      responses: {200: unknown,
},
      
    }
export type get__api_session = {
      method: "GET",
      path: "/api/session",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.SessionView,
},
      
    }
export type put__api_session_language = {
      method: "PUT",
      path: "/api/session/language",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.LanguageChangeRequest,
          }
      responses: {204: unknown,
},
      
    }
export type post__api_orders = {
      method: "POST",
      path: "/api/orders",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.PlaceOrderRequest,
          }
      responses: {201: Schemas.PlacedOrderView,
},
      
    }
export type post__api_openItems_tables_TableId_search = {
      method: "POST",
      path: "/api/open-items/tables/{tableId}/search",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { tableId: string },
        
        
        body:  Schemas.TableOrderSearchRequest,
          }
      responses: {200: Schemas.TableOrderReportView,
},
      
    }
export type get__api_openItems_items_OrderItemId_history = {
      method: "GET",
      path: "/api/open-items/items/{orderItemId}/history",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { orderItemId: string },
        
        
        
          }
      responses: {200: Schemas.OrderItemHistoryView,
},
      
    }
export type post__api_openItems_settle = {
      method: "POST",
      path: "/api/open-items/settle",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.SettleItemsRequest,
          }
      responses: {200: Schemas.SettlementView,
},
      
    }
export type post__api_openItems_cancel = {
      method: "POST",
      path: "/api/open-items/cancel",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.CancelItemsRequest,
          }
      responses: {200: Schemas.CancellationView,
},
      
    }
export type get__api_estimates = {
      method: "GET",
      path: "/api/estimates",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Array<Schemas.ItemEstimateView>,
},
      
    }
export type post__api_estimates_quote = {
      method: "POST",
      path: "/api/estimates/quote",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.EstimateQuoteRequest,
          }
      responses: {200: Schemas.EstimateQuoteView,
},
      
    }
export type get__api_station_orders = {
      method: "GET",
      path: "/api/station/orders",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.StationQueueView,
},
      
    }
export type get__api_station_orders_fulfilled = {
      method: "GET",
      path: "/api/station/orders/fulfilled",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.StationFulfilledView,
},
      
    }
export type post__api_station_items_fulfill = {
      method: "POST",
      path: "/api/station/items/fulfill",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.StationItemSelectionRequest,
          }
      responses: {200: Schemas.StationQueueView,
},
      
    }
export type post__api_station_items_unfulfill = {
      method: "POST",
      path: "/api/station/items/unfulfill",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.StationItemSelectionRequest,
          }
      responses: {200: Schemas.StationQueueView,
},
      
    }
export type post__api_station_items_acknowledgeCancellation = {
      method: "POST",
      path: "/api/station/items/acknowledge-cancellation",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.StationItemSelectionRequest,
          }
      responses: {200: Schemas.StationQueueView,
},
      
    }
export type post__api_station_orders_StationOrderId_hide = {
      method: "POST",
      path: "/api/station/orders/{stationOrderId}/hide",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { stationOrderId: string },
        
        
        
          }
      responses: {200: Schemas.StationQueueView,
},
      
    }
export type get__api_admin_stations = {
      method: "GET",
      path: "/api/admin/stations",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            query?:  Partial<{ festivalId: string }>,
        
        
        
        
          }
      responses: {200: Schemas.AdminStationListView,
},
      
    }
export type post__api_admin_stations = {
      method: "POST",
      path: "/api/admin/stations",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.SaveStationRequest,
          }
      responses: {201: Schemas.AdminStationView,
},
      
    }
export type put__api_admin_stations_StationId = {
      method: "PUT",
      path: "/api/admin/stations/{stationId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { stationId: string },
        
        
        body:  Schemas.SaveStationRequest,
          }
      responses: {200: Schemas.SavedStationView,
},
      
    }
export type post__api_admin_stations_StationId_deactivate = {
      method: "POST",
      path: "/api/admin/stations/{stationId}/deactivate",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { stationId: string },
        
        
        
          }
      responses: {200: Schemas.SavedStationView,
},
      
    }
export type post__api_admin_stations_StationId_activate = {
      method: "POST",
      path: "/api/admin/stations/{stationId}/activate",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { stationId: string },
        
        
        
          }
      responses: {200: Schemas.SavedStationView,
},
      
    }
export type get__api_admin_categories = {
      method: "GET",
      path: "/api/admin/categories",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.AdminCategoryListView,
},
      
    }
export type post__api_admin_categories = {
      method: "POST",
      path: "/api/admin/categories",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.SaveCategoryRequest,
          }
      responses: {201: Schemas.AdminCategoryView,
},
      
    }
export type put__api_admin_categories_CategoryId = {
      method: "PUT",
      path: "/api/admin/categories/{categoryId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { categoryId: string },
        
        
        body:  Schemas.SaveCategoryRequest,
          }
      responses: {200: Schemas.AdminCategoryView,
},
      
    }
export type post__api_admin_categories_CategoryId_move = {
      method: "POST",
      path: "/api/admin/categories/{categoryId}/move",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { categoryId: string },
        
        
        body:  Schemas.MoveCategoryRequest,
          }
      responses: {200: Schemas.AdminCategoryListView,
},
      
    }
export type post__api_admin_categories_CategoryId_activate = {
      method: "POST",
      path: "/api/admin/categories/{categoryId}/activate",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { categoryId: string },
        
        
        
          }
      responses: {200: Schemas.AdminCategoryView,
},
      
    }
export type post__api_admin_categories_CategoryId_deactivate = {
      method: "POST",
      path: "/api/admin/categories/{categoryId}/deactivate",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { categoryId: string },
        
        
        
          }
      responses: {200: Schemas.AdminCategoryView,
},
      
    }
export type get__api_admin_items = {
      method: "GET",
      path: "/api/admin/items",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            query?:  Partial<{ festivalId: string }>,
        
        
        
        
          }
      responses: {200: Schemas.AdminItemListView,
},
      
    }
export type post__api_admin_items = {
      method: "POST",
      path: "/api/admin/items",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.SaveItemRequest,
          }
      responses: {201: Schemas.AdminItemView,
},
      
    }
export type put__api_admin_items_ItemId = {
      method: "PUT",
      path: "/api/admin/items/{itemId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { itemId: string },
        
        
        body:  Schemas.SaveItemRequest,
          }
      responses: {200: Schemas.SavedItemView,
},
      
    }
export type post__api_admin_items_ItemId_activate = {
      method: "POST",
      path: "/api/admin/items/{itemId}/activate",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { itemId: string },
        
        
        
          }
      responses: {200: Schemas.SavedItemView,
},
      
    }
export type post__api_admin_items_ItemId_deactivate = {
      method: "POST",
      path: "/api/admin/items/{itemId}/deactivate",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { itemId: string },
        
        
        
          }
      responses: {200: Schemas.SavedItemView,
},
      
    }
export type put__api_admin_items_ItemId_ingredients_IngredientId = {
      method: "PUT",
      path: "/api/admin/items/{itemId}/ingredients/{ingredientId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { itemId: string, ingredientId: string },
        
        
        body:  Schemas.SaveItemIngredientRequest,
          }
      responses: {200: Schemas.SavedItemView,
},
      
    }
export type delete__api_admin_items_ItemId_ingredients_IngredientId = {
      method: "DELETE",
      path: "/api/admin/items/{itemId}/ingredients/{ingredientId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { itemId: string, ingredientId: string },
        
        
        
          }
      responses: {204: unknown,
},
      
    }
export type get__api_admin_staffMembers = {
      method: "GET",
      path: "/api/admin/staff-members",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.AdminStaffMemberListView,
},
      
    }
export type put__api_admin_staffMembers_StaffMemberId = {
      method: "PUT",
      path: "/api/admin/staff-members/{staffMemberId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { staffMemberId: string },
        
        
        body:  Schemas.RenameStaffMemberRequest,
          }
      responses: {200: Schemas.StaffMemberView,
},
      
    }
export type post__api_admin_staffMembers_StaffMemberId_activate = {
      method: "POST",
      path: "/api/admin/staff-members/{staffMemberId}/activate",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { staffMemberId: string },
        
        
        
          }
      responses: {200: Schemas.StaffMemberView,
},
      
    }
export type post__api_admin_staffMembers_StaffMemberId_deactivate = {
      method: "POST",
      path: "/api/admin/staff-members/{staffMemberId}/deactivate",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { staffMemberId: string },
        
        
        
          }
      responses: {200: Schemas.StaffMemberView,
},
      
    }
export type get__api_admin_festivals = {
      method: "GET",
      path: "/api/admin/festivals",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.AdminFestivalListView,
},
      
    }
export type post__api_admin_festivals = {
      method: "POST",
      path: "/api/admin/festivals",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.SaveFestivalRequest,
          }
      responses: {201: Schemas.SavedFestivalView,
},
      
    }
export type put__api_admin_festivals_FestivalId = {
      method: "PUT",
      path: "/api/admin/festivals/{festivalId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        body:  Schemas.SaveFestivalRequest,
          }
      responses: {200: Schemas.SavedFestivalView,
},
      
    }
export type post__api_admin_festivals_FestivalId_copy = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/copy",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        body:  Schemas.SaveFestivalRequest,
          }
      responses: {201: Schemas.SavedFestivalView,
},
      
    }
export type post__api_admin_festivals_FestivalId_hide = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/hide",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        
          }
      responses: {200: Schemas.SavedFestivalView,
},
      
    }
export type post__api_admin_festivals_FestivalId_show = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/show",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        
          }
      responses: {200: Schemas.SavedFestivalView,
},
      
    }
export type put__api_admin_festivals_FestivalId_items_ItemId = {
      method: "PUT",
      path: "/api/admin/festivals/{festivalId}/items/{itemId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, itemId: string },
        
        
        body:  Schemas.SaveFestivalItemRequest,
          }
      responses: {200: Schemas.SavedItemView,
},
      
    }
export type delete__api_admin_festivals_FestivalId_items_ItemId = {
      method: "DELETE",
      path: "/api/admin/festivals/{festivalId}/items/{itemId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, itemId: string },
        
        
        
          }
      responses: {204: unknown,
},
      
    }
export type post__api_admin_festivals_FestivalId_items_ItemId_availability = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/items/{itemId}/availability",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, itemId: string },
        
        
        body:  Schemas.SetAvailabilityRequest,
          }
      responses: {200: Schemas.SavedItemView,
},
      
    }
export type put__api_admin_festivals_FestivalId_stations_StationId = {
      method: "PUT",
      path: "/api/admin/festivals/{festivalId}/stations/{stationId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, stationId: string },
        
        
        
          }
      responses: {200: Schemas.SavedStationView,
},
      
    }
export type delete__api_admin_festivals_FestivalId_stations_StationId = {
      method: "DELETE",
      path: "/api/admin/festivals/{festivalId}/stations/{stationId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, stationId: string },
        
        
        
          }
      responses: {204: unknown,
},
      
    }
export type get__api_admin_festivals_FestivalId_ingredients = {
      method: "GET",
      path: "/api/admin/festivals/{festivalId}/ingredients",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        
          }
      responses: {200: Schemas.AdminFestivalIngredientListView,
},
      
    }
export type put__api_admin_festivals_FestivalId_ingredients_IngredientId = {
      method: "PUT",
      path: "/api/admin/festivals/{festivalId}/ingredients/{ingredientId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, ingredientId: string },
        
        
        body:  Schemas.SaveFestivalIngredientRequest,
          }
      responses: {200: Schemas.SavedFestivalIngredientView,
},
      
    }
export type get__api_admin_festivals_FestivalId_tablePlan = {
      method: "GET",
      path: "/api/admin/festivals/{festivalId}/table-plan",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        
          }
      responses: {200: Schemas.AdminTablePlanView,
},
      
    }
export type post__api_admin_festivals_FestivalId_areas = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/areas",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        body:  Schemas.SaveAreaRequest,
          }
      responses: {201: Schemas.SavedAreaView,
},
      
    }
export type put__api_admin_festivals_FestivalId_areas_AreaId = {
      method: "PUT",
      path: "/api/admin/festivals/{festivalId}/areas/{areaId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, areaId: string },
        
        
        body:  Schemas.SaveAreaRequest,
          }
      responses: {200: Schemas.SavedAreaView,
},
      
    }
export type post__api_admin_festivals_FestivalId_areas_AreaId_move = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/areas/{areaId}/move",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, areaId: string },
        
        
        body:  Schemas.MoveAreaRequest,
          }
      responses: {200: Schemas.SavedAreaView,
},
      
    }
export type post__api_admin_festivals_FestivalId_areas_AreaId_hide = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/areas/{areaId}/hide",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, areaId: string },
        
        
        
          }
      responses: {200: Schemas.SavedAreaView,
},
      
    }
export type post__api_admin_festivals_FestivalId_areas_AreaId_show = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/areas/{areaId}/show",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, areaId: string },
        
        
        
          }
      responses: {200: Schemas.SavedAreaView,
},
      
    }
export type post__api_admin_festivals_FestivalId_tables = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/tables",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        body:  Schemas.SaveTableRequest,
          }
      responses: {201: Schemas.SavedTableView,
},
      
    }
export type post__api_admin_festivals_FestivalId_tables_copy = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/tables/copy",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        body:  Schemas.CopyTablesRequest,
          }
      responses: {201: Schemas.CopiedTablesView,
},
      
    }
export type post__api_admin_festivals_FestivalId_tables_move = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/tables/move",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string },
        
        
        body:  Schemas.MoveTablesRequest,
          }
      responses: {204: unknown,
},
      
    }
export type put__api_admin_festivals_FestivalId_tables_TableId = {
      method: "PUT",
      path: "/api/admin/festivals/{festivalId}/tables/{tableId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, tableId: string },
        
        
        body:  Schemas.SaveTableRequest,
          }
      responses: {200: Schemas.SavedTableView,
},
      
    }
export type post__api_admin_festivals_FestivalId_tables_TableId_hide = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/tables/{tableId}/hide",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, tableId: string },
        
        
        
          }
      responses: {200: Schemas.SavedTableView,
},
      
    }
export type post__api_admin_festivals_FestivalId_tables_TableId_show = {
      method: "POST",
      path: "/api/admin/festivals/{festivalId}/tables/{tableId}/show",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, tableId: string },
        
        
        
          }
      responses: {200: Schemas.SavedTableView,
},
      
    }
export type put__api_admin_festivals_FestivalId_stations_StationId_shape = {
      method: "PUT",
      path: "/api/admin/festivals/{festivalId}/stations/{stationId}/shape",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, stationId: string },
        
        
        body:  Schemas.SaveStationShapeRequest,
          }
      responses: {200: Schemas.SavedStationView,
},
      
    }
export type delete__api_admin_festivals_FestivalId_stations_StationId_shape = {
      method: "DELETE",
      path: "/api/admin/festivals/{festivalId}/stations/{stationId}/shape",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { festivalId: string, stationId: string },
        
        
        
          }
      responses: {204: unknown,
},
      
    }
export type get__api_admin_ingredients = {
      method: "GET",
      path: "/api/admin/ingredients",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.AdminIngredientListView,
},
      
    }
export type post__api_admin_ingredients = {
      method: "POST",
      path: "/api/admin/ingredients",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.SaveIngredientRequest,
          }
      responses: {201: Schemas.AdminIngredientView,
},
      
    }
export type put__api_admin_ingredients_IngredientId = {
      method: "PUT",
      path: "/api/admin/ingredients/{ingredientId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { ingredientId: string },
        
        
        body:  Schemas.SaveIngredientRequest,
          }
      responses: {200: Schemas.SavedIngredientView,
},
      
    }
export type delete__api_admin_ingredients_IngredientId = {
      method: "DELETE",
      path: "/api/admin/ingredients/{ingredientId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { ingredientId: string },
        
        
        
          }
      responses: {204: unknown,
},
      
    }
export type get__api_admin_units = {
      method: "GET",
      path: "/api/admin/units",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.AdminUnitListView,
},
      
    }
export type post__api_admin_units = {
      method: "POST",
      path: "/api/admin/units",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        
        
        
        body:  Schemas.SaveUnitRequest,
          }
      responses: {201: Schemas.AdminUnitView,
},
      
    }
export type put__api_admin_units_UnitId = {
      method: "PUT",
      path: "/api/admin/units/{unitId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { unitId: string },
        
        
        body:  Schemas.SaveUnitRequest,
          }
      responses: {200: Schemas.SavedUnitView,
},
      
    }
export type delete__api_admin_units_UnitId = {
      method: "DELETE",
      path: "/api/admin/units/{unitId}",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            
        path:  { unitId: string },
        
        
        
          }
      responses: {204: unknown,
},
      
    }

  // </Endpoints>
  }
  
  
     // <EndpointByMethod>
     export type EndpointByMethod = {
     post: {
           "/api/enrolment/redeem": Endpoints.post__api_enrolment_redeem,
"/api/admin/enrolment/invitations": Endpoints.post__api_admin_enrolment_invitations,
"/api/orders": Endpoints.post__api_orders,
"/api/open-items/tables/{tableId}/search": Endpoints.post__api_openItems_tables_TableId_search,
"/api/open-items/settle": Endpoints.post__api_openItems_settle,
"/api/open-items/cancel": Endpoints.post__api_openItems_cancel,
"/api/estimates/quote": Endpoints.post__api_estimates_quote,
"/api/station/items/fulfill": Endpoints.post__api_station_items_fulfill,
"/api/station/items/unfulfill": Endpoints.post__api_station_items_unfulfill,
"/api/station/items/acknowledge-cancellation": Endpoints.post__api_station_items_acknowledgeCancellation,
"/api/station/orders/{stationOrderId}/hide": Endpoints.post__api_station_orders_StationOrderId_hide,
"/api/admin/stations": Endpoints.post__api_admin_stations,
"/api/admin/stations/{stationId}/deactivate": Endpoints.post__api_admin_stations_StationId_deactivate,
"/api/admin/stations/{stationId}/activate": Endpoints.post__api_admin_stations_StationId_activate,
"/api/admin/categories": Endpoints.post__api_admin_categories,
"/api/admin/categories/{categoryId}/move": Endpoints.post__api_admin_categories_CategoryId_move,
"/api/admin/categories/{categoryId}/activate": Endpoints.post__api_admin_categories_CategoryId_activate,
"/api/admin/categories/{categoryId}/deactivate": Endpoints.post__api_admin_categories_CategoryId_deactivate,
"/api/admin/items": Endpoints.post__api_admin_items,
"/api/admin/items/{itemId}/activate": Endpoints.post__api_admin_items_ItemId_activate,
"/api/admin/items/{itemId}/deactivate": Endpoints.post__api_admin_items_ItemId_deactivate,
"/api/admin/staff-members/{staffMemberId}/activate": Endpoints.post__api_admin_staffMembers_StaffMemberId_activate,
"/api/admin/staff-members/{staffMemberId}/deactivate": Endpoints.post__api_admin_staffMembers_StaffMemberId_deactivate,
"/api/admin/festivals": Endpoints.post__api_admin_festivals,
"/api/admin/festivals/{festivalId}/copy": Endpoints.post__api_admin_festivals_FestivalId_copy,
"/api/admin/festivals/{festivalId}/hide": Endpoints.post__api_admin_festivals_FestivalId_hide,
"/api/admin/festivals/{festivalId}/show": Endpoints.post__api_admin_festivals_FestivalId_show,
"/api/admin/festivals/{festivalId}/items/{itemId}/availability": Endpoints.post__api_admin_festivals_FestivalId_items_ItemId_availability,
"/api/admin/festivals/{festivalId}/areas": Endpoints.post__api_admin_festivals_FestivalId_areas,
"/api/admin/festivals/{festivalId}/areas/{areaId}/move": Endpoints.post__api_admin_festivals_FestivalId_areas_AreaId_move,
"/api/admin/festivals/{festivalId}/areas/{areaId}/hide": Endpoints.post__api_admin_festivals_FestivalId_areas_AreaId_hide,
"/api/admin/festivals/{festivalId}/areas/{areaId}/show": Endpoints.post__api_admin_festivals_FestivalId_areas_AreaId_show,
"/api/admin/festivals/{festivalId}/tables": Endpoints.post__api_admin_festivals_FestivalId_tables,
"/api/admin/festivals/{festivalId}/tables/copy": Endpoints.post__api_admin_festivals_FestivalId_tables_copy,
"/api/admin/festivals/{festivalId}/tables/move": Endpoints.post__api_admin_festivals_FestivalId_tables_move,
"/api/admin/festivals/{festivalId}/tables/{tableId}/hide": Endpoints.post__api_admin_festivals_FestivalId_tables_TableId_hide,
"/api/admin/festivals/{festivalId}/tables/{tableId}/show": Endpoints.post__api_admin_festivals_FestivalId_tables_TableId_show,
"/api/admin/ingredients": Endpoints.post__api_admin_ingredients,
"/api/admin/units": Endpoints.post__api_admin_units
         },
get: {
           "/api/catalog": Endpoints.get__api_catalog,
"/api/table-plan": Endpoints.get__api_tablePlan,
"/api/language": Endpoints.get__api_language,
"/api/admin/enrolment/invitations/{invitationId}/qr.svg": Endpoints.get__api_admin_enrolment_invitations_InvitationId_qr_svg,
"/api/session": Endpoints.get__api_session,
"/api/open-items/items/{orderItemId}/history": Endpoints.get__api_openItems_items_OrderItemId_history,
"/api/estimates": Endpoints.get__api_estimates,
"/api/station/orders": Endpoints.get__api_station_orders,
"/api/station/orders/fulfilled": Endpoints.get__api_station_orders_fulfilled,
"/api/admin/stations": Endpoints.get__api_admin_stations,
"/api/admin/categories": Endpoints.get__api_admin_categories,
"/api/admin/items": Endpoints.get__api_admin_items,
"/api/admin/staff-members": Endpoints.get__api_admin_staffMembers,
"/api/admin/festivals": Endpoints.get__api_admin_festivals,
"/api/admin/festivals/{festivalId}/ingredients": Endpoints.get__api_admin_festivals_FestivalId_ingredients,
"/api/admin/festivals/{festivalId}/table-plan": Endpoints.get__api_admin_festivals_FestivalId_tablePlan,
"/api/admin/ingredients": Endpoints.get__api_admin_ingredients,
"/api/admin/units": Endpoints.get__api_admin_units
         },
put: {
           "/api/session/language": Endpoints.put__api_session_language,
"/api/admin/stations/{stationId}": Endpoints.put__api_admin_stations_StationId,
"/api/admin/categories/{categoryId}": Endpoints.put__api_admin_categories_CategoryId,
"/api/admin/items/{itemId}": Endpoints.put__api_admin_items_ItemId,
"/api/admin/items/{itemId}/ingredients/{ingredientId}": Endpoints.put__api_admin_items_ItemId_ingredients_IngredientId,
"/api/admin/staff-members/{staffMemberId}": Endpoints.put__api_admin_staffMembers_StaffMemberId,
"/api/admin/festivals/{festivalId}": Endpoints.put__api_admin_festivals_FestivalId,
"/api/admin/festivals/{festivalId}/items/{itemId}": Endpoints.put__api_admin_festivals_FestivalId_items_ItemId,
"/api/admin/festivals/{festivalId}/stations/{stationId}": Endpoints.put__api_admin_festivals_FestivalId_stations_StationId,
"/api/admin/festivals/{festivalId}/ingredients/{ingredientId}": Endpoints.put__api_admin_festivals_FestivalId_ingredients_IngredientId,
"/api/admin/festivals/{festivalId}/areas/{areaId}": Endpoints.put__api_admin_festivals_FestivalId_areas_AreaId,
"/api/admin/festivals/{festivalId}/tables/{tableId}": Endpoints.put__api_admin_festivals_FestivalId_tables_TableId,
"/api/admin/festivals/{festivalId}/stations/{stationId}/shape": Endpoints.put__api_admin_festivals_FestivalId_stations_StationId_shape,
"/api/admin/ingredients/{ingredientId}": Endpoints.put__api_admin_ingredients_IngredientId,
"/api/admin/units/{unitId}": Endpoints.put__api_admin_units_UnitId
         },
delete: {
           "/api/admin/items/{itemId}/ingredients/{ingredientId}": Endpoints.delete__api_admin_items_ItemId_ingredients_IngredientId,
"/api/admin/festivals/{festivalId}/items/{itemId}": Endpoints.delete__api_admin_festivals_FestivalId_items_ItemId,
"/api/admin/festivals/{festivalId}/stations/{stationId}": Endpoints.delete__api_admin_festivals_FestivalId_stations_StationId,
"/api/admin/festivals/{festivalId}/stations/{stationId}/shape": Endpoints.delete__api_admin_festivals_FestivalId_stations_StationId_shape,
"/api/admin/ingredients/{ingredientId}": Endpoints.delete__api_admin_ingredients_IngredientId,
"/api/admin/units/{unitId}": Endpoints.delete__api_admin_units_UnitId
         }
     }
     
     // </EndpointByMethod>
     

    // <EndpointByMethod.Shorthands>
    export type PostEndpoints = EndpointByMethod["post"]
export type GetEndpoints = EndpointByMethod["get"]
export type PutEndpoints = EndpointByMethod["put"]
export type DeleteEndpoints = EndpointByMethod["delete"]
    // </EndpointByMethod.Shorthands>
    