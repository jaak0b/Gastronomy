import { surfaceDefaults, type VuetifyDefaults } from '../../shared/styles/appVuetify'

export const ADMIN_SIZES_CLASS = 'admin-sizes'

const ADMIN_CONTROL_DEFAULTS: VuetifyDefaults = {
  VAlert: { density: 'compact' },
  VAutocomplete: { density: 'compact' },
  VBtn: { size: 'default', density: 'comfortable' },
  VBtnToggle: { density: 'compact' },
  VCheckbox: { density: 'compact' },
  VChip: { size: 'small' },
  VCombobox: { density: 'compact' },
  VList: { density: 'compact' },
  VListItem: { density: 'compact' },
  VNumberInput: { density: 'compact' },
  VRadioGroup: { density: 'compact' },
  VSelect: { density: 'compact' },
  VSwitch: { density: 'compact' },
  VTable: { density: 'compact' },
  VTabs: { density: 'compact' },
  VTextarea: { density: 'compact' },
  VTextField: { density: 'compact' },
  VToolbar: { density: 'compact' },
}

export const ADMIN_DEFAULTS: VuetifyDefaults = surfaceDefaults(ADMIN_SIZES_CLASS, ADMIN_CONTROL_DEFAULTS)
