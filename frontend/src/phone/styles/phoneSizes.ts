import { surfaceDefaults, type VuetifyDefaults } from '../../shared/styles/appVuetify'

export const PHONE_SIZES_CLASS = 'phone-sizes'

const PHONE_CONTROL_DEFAULTS: VuetifyDefaults = {
  VAppBar: { height: 72 },
  VBtn: { size: 'large' },
  VBtnWide: { size: 'x-large', variant: 'flat', block: true },
  VCheckbox: { density: 'comfortable' },
  VList: { density: 'comfortable' },
  VNumberInput: { density: 'comfortable' },
  VProgressCircular: { size: 48 },
  VSelect: { density: 'comfortable' },
  VTextField: { density: 'comfortable' },
}

export const PHONE_DEFAULTS: VuetifyDefaults = surfaceDefaults(PHONE_SIZES_CLASS, PHONE_CONTROL_DEFAULTS)

export const PHONE_NOTICE_BAR_DEFAULTS: VuetifyDefaults = {
  VAlert: { density: 'compact', rounded: 0 },
}

export const PHONE_ORDER_GRID_DEFAULTS: VuetifyDefaults = {
  VBtn: { density: 'comfortable' },
  VCheckboxBtn: { density: 'compact' },
}
