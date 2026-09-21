
  export namespace Schemas {
    // <Schemas>
  export type AdminCategoryView = { categoryId: string, name: string, colourHex: string, sortOrder: number, isActive: boolean }
export type AdminCategoryListView = { categories: Array<AdminCategoryView> }
export type AdminFestivalView = { festivalId: string, name: string, startsAtUtc: string, endsAtUtc: string, isHidden: boolean, isRunning: boolean, stationCount: number, menuItemCount: number, orderCount: number }
export type AdminFestivalListView = { festivals: Array<AdminFestivalView> }
export type AdminItemAtFestivalView = { priceCents: number, isAvailable: boolean, stationIds: Array<string> }
export type AdminItemView = { itemId: string, name: string, categoryId: string, sortOrder: number, isActive: boolean, productionMinutes: (null | number), isQueueIndependent: boolean, atTheFestival: (null | AdminItemAtFestivalView) }
export type AdminItemListView = { items: Array<AdminItemView> }
export type AdminStaffMemberView = { staffMemberId: string, name: string, isActive: boolean, hasDevice: boolean, lastSeenAtUtc: (null | string), hasOutstandingInvitation: boolean }
export type AdminStaffMemberListView = { staffMembers: Array<AdminStaffMemberView> }
export type AdminStationView = { stationId: string, name: string, sortOrder: number, isActive: boolean, hasDevice: boolean, lastSeenAtUtc: (null | string), hasOutstandingInvitation: boolean, isAtTheFestival: boolean }
export type AdminStationListView = { stations: Array<AdminStationView> }
export type CatalogCategoryView = { categoryId: string, name: string, colourHex: string, sortOrder: number }
export type CatalogChangedEvent = Record<string, unknown>
export type CatalogItemView = { id: string, categoryId: string, name: string, priceCents: number, sortOrder: number, isAvailable: boolean, productionMinutes: (null | number), isQueueIndependent: boolean, stationIds: Array<string> }
export type CatalogStationView = { id: string, name: string, sortOrder: number }
export type RunningFestivalView = { festivalId: string, name: string }
export type CatalogView = { festival: (null | RunningFestivalView), categories: Array<CatalogCategoryView>, items: Array<CatalogItemView>, stations: Array<CatalogStationView> }
export type CategoryMoveDirection = ("up" | "down")
export type CreateInvitationRequest = Partial<{ staffMemberId: (null | string), stationId: (null | string) }>
export type DeliveryMode = ("together" | "asItComes")
export type DeviceOwnerKind = ("staffMember" | "station")
export type DeviceRevokedEvent = { deviceId: string }
export type EnrolmentCompletedEvent = { deviceKind: ("staffMember" | "station"), ownerId: string, ownerName: string, deviceId: string }
export type FestivalChangedEvent = Record<string, unknown>
export type StaffMemberView = { id: string, name: string }
export type StationSummaryView = { id: string, name: string }
export type InvitationView = { invitationId: string, qrUrl: string, expiresAtUtc: string, ownerKind: (null | DeviceOwnerKind), staffMember: (null | StaffMemberView), station: (null | StationSummaryView), availableAddresses: Array<string> }
export type LanguageChangeRequest = { language: (null | string) }
export type LanguageView = { language: string }
export type MoveCategoryRequest = { direction: CategoryMoveDirection }
export type OpenOrderItemView = { orderItemId: string, orderId: string, globalOrderNumber: number, itemName: string, note: (null | string), unitPriceCents: number, orderedAtUtc: string }
export type OpenTableView = { tableName: string, openAmountCents: number, items: Array<OpenOrderItemView> }
export type OpenItemsView = { tables: Array<OpenTableView>, itemsWithoutAnOrderCount: number }
export type OrderDeliveryModeRequest = { stationId: string, deliveryMode: DeliveryMode }
export type OrderSettlementLineRequest = { paidPriceCents: (null | number), paymentNotice?: (null | string) }
export type OrderItemRequest = { catalogItemId: string, unitPriceCents: number, note?: (null | string), stationId?: (null | string), settlement?: (null | OrderSettlementLineRequest) }
export type OrderItemsSettledEvent = { orderItemIds: Array<string>, tableNames: Array<string> }
export type OrderStatus = ("open" | "partiallyFulfilled" | "fulfilled")
export type OrderStatusChangedEvent = { orderId: string, status: ("open" | "partiallyFulfilled" | "fulfilled") }
export type StationOrderView = { stationOrderId: string, stationId: string, stationName: string, stationOrderNumber: number, deliveryMode: DeliveryMode, itemIds: Array<string> }
export type PlacedOrderView = { orderId: string, globalOrderNumber: number, status: OrderStatus, totalCents: number, createdAtUtc: string, stationOrders: Array<StationOrderView> }
export type PlaceOrderRequest = { clientOrderId: string, tableName: (null | string), items: (null | Array<OrderItemRequest>), deliveryModes?: (null | Array<OrderDeliveryModeRequest>) }
export type RedeemedEnrolmentView = { deviceId: string, deviceToken: string, deviceKind: DeviceOwnerKind, staffMember: (null | StaffMemberView), station: (null | StationSummaryView), language: string }
export type RedeemEnrolmentRequest = Partial<{ code: (null | string), name: (null | string), userAgent: (null | string), previousDeviceToken: (null | string) }>
export type RenameStaffMemberRequest = { name: (null | string) }
export type SaveCategoryRequest = { name: (null | string), colourHex: (null | string) }
export type SavedFestivalView = { festivalId: string }
export type SavedItemView = { itemId: string }
export type SavedStationView = { stationId: string }
export type SaveFestivalItemRequest = { priceCents: number, stationIds: (null | Array<string>) }
export type SaveFestivalRequest = { name: (null | string), startsAtUtc: string, endsAtUtc: string }
export type SaveItemRequest = { name: (null | string), categoryId: (null | string), sortOrder: number, productionMinutes?: (null | number), isQueueIndependent?: boolean }
export type SaveStationRequest = { name: (null | string), sortOrder: number }
export type SessionView = { deviceId: string, deviceKind: DeviceOwnerKind, staffMember: (null | StaffMemberView), station: (null | StationSummaryView), language: string }
export type SetAvailabilityRequest = { isAvailable: boolean }
export type SettleLineRequest = { orderItemId: string, paidPriceCents: (null | number), paymentNotice?: (null | string) }
export type SettleItemsRequest = { lines: (null | Array<SettleLineRequest>) }
export type SettlementView = { settledOrderItemIds: Array<string>, reappliedOrderItemIds: Array<string>, alreadySettledByOthersOrderItemIds: Array<string>, otherPhonesWereTold: boolean }
export type StationEstimateView = { stationId: string, queuedMinutes: number }
export type StationEstimateListView = { stations: Array<StationEstimateView> }
export type StationQueueItemView = { orderItemId: string, itemName: string, note: (null | string), fulfilledAtUtc: (null | string) }
export type StationOrderQueueView = { stationOrderId: string, globalOrderNumber: number, stationOrderNumber: number, tableName: string, staffMemberName: string, deliveryMode: DeliveryMode, createdAtUtc: string, isHiddenFromAsItComesQueue: boolean, itemCount: number, fulfilledItemCount: number, items: Array<StationQueueItemView> }
export type StationFulfilledView = { stationOrders: Array<StationOrderQueueView> }
export type StationItemSelectionRequest = { orderItemIds: (null | Array<string>) }
export type StationOrdersChangedEvent = { stationId: string }
export type StationQueueView = { station: StationSummaryView, orders: Array<StationOrderQueueView>, asItComes: Array<StationOrderQueueView> }
export type StationsChangedEvent = Record<string, unknown>
export type TableNamesView = { tableNames: Array<string> }
export type TableOrderRecordItemView = { orderItemId: string, orderId: string, globalOrderNumber: number, itemName: string, note: (null | string), unitPriceCents: number, orderedAtUtc: string, fulfilledAtUtc: (null | string), settledAtUtc: (null | string) }
export type TableOrderRecordView = { orderId: string, globalOrderNumber: number, createdAtUtc: string, staffMemberName: string, items: Array<TableOrderRecordItemView> }
export type TableOrderReportView = { tableName: string, openAmountCents: number, orders: Array<TableOrderRecordView> }

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
export type get__api_openItems = {
      method: "GET",
      path: "/api/open-items",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.OpenItemsView,
},
      
    }
export type get__api_openItems_tableNames = {
      method: "GET",
      path: "/api/open-items/table-names",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.TableNamesView,
},
      
    }
export type get__api_openItems_table = {
      method: "GET",
      path: "/api/open-items/table",
      requestFormat: "json",
      responseFormat: "json",
      parameters: {
            query?:  Partial<{ tableName: string }>,
        
        
        
        
          }
      responses: {200: Schemas.TableOrderReportView,
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
export type get__api_estimates = {
      method: "GET",
      path: "/api/estimates",
      requestFormat: "json",
      responseFormat: "json",
      parameters: never,
      responses: {200: Schemas.StationEstimateListView,
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

  // </Endpoints>
  }
  
  
     // <EndpointByMethod>
     export type EndpointByMethod = {
     post: {
           "/api/enrolment/redeem": Endpoints.post__api_enrolment_redeem,
"/api/admin/enrolment/invitations": Endpoints.post__api_admin_enrolment_invitations,
"/api/orders": Endpoints.post__api_orders,
"/api/open-items/settle": Endpoints.post__api_openItems_settle,
"/api/station/items/fulfill": Endpoints.post__api_station_items_fulfill,
"/api/station/items/unfulfill": Endpoints.post__api_station_items_unfulfill,
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
"/api/admin/festivals/{festivalId}/items/{itemId}/availability": Endpoints.post__api_admin_festivals_FestivalId_items_ItemId_availability
         },
get: {
           "/api/catalog": Endpoints.get__api_catalog,
"/api/language": Endpoints.get__api_language,
"/api/admin/enrolment/invitations/{invitationId}/qr.svg": Endpoints.get__api_admin_enrolment_invitations_InvitationId_qr_svg,
"/api/session": Endpoints.get__api_session,
"/api/open-items": Endpoints.get__api_openItems,
"/api/open-items/table-names": Endpoints.get__api_openItems_tableNames,
"/api/open-items/table": Endpoints.get__api_openItems_table,
"/api/estimates": Endpoints.get__api_estimates,
"/api/station/orders": Endpoints.get__api_station_orders,
"/api/station/orders/fulfilled": Endpoints.get__api_station_orders_fulfilled,
"/api/admin/stations": Endpoints.get__api_admin_stations,
"/api/admin/categories": Endpoints.get__api_admin_categories,
"/api/admin/items": Endpoints.get__api_admin_items,
"/api/admin/staff-members": Endpoints.get__api_admin_staffMembers,
"/api/admin/festivals": Endpoints.get__api_admin_festivals
         },
put: {
           "/api/session/language": Endpoints.put__api_session_language,
"/api/admin/stations/{stationId}": Endpoints.put__api_admin_stations_StationId,
"/api/admin/categories/{categoryId}": Endpoints.put__api_admin_categories_CategoryId,
"/api/admin/items/{itemId}": Endpoints.put__api_admin_items_ItemId,
"/api/admin/staff-members/{staffMemberId}": Endpoints.put__api_admin_staffMembers_StaffMemberId,
"/api/admin/festivals/{festivalId}": Endpoints.put__api_admin_festivals_FestivalId,
"/api/admin/festivals/{festivalId}/items/{itemId}": Endpoints.put__api_admin_festivals_FestivalId_items_ItemId,
"/api/admin/festivals/{festivalId}/stations/{stationId}": Endpoints.put__api_admin_festivals_FestivalId_stations_StationId
         },
delete: {
           "/api/admin/festivals/{festivalId}/items/{itemId}": Endpoints.delete__api_admin_festivals_FestivalId_items_ItemId,
"/api/admin/festivals/{festivalId}/stations/{stationId}": Endpoints.delete__api_admin_festivals_FestivalId_stations_StationId
         }
     }
     
     // </EndpointByMethod>
     

    // <EndpointByMethod.Shorthands>
    export type PostEndpoints = EndpointByMethod["post"]
export type GetEndpoints = EndpointByMethod["get"]
export type PutEndpoints = EndpointByMethod["put"]
export type DeleteEndpoints = EndpointByMethod["delete"]
    // </EndpointByMethod.Shorthands>
    