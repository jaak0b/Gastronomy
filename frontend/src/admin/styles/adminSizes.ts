import type { createVuetify } from 'vuetify'

type VuetifyDefaults = NonNullable<NonNullable<Parameters<typeof createVuetify>[0]>['defaults']>

export const ADMIN_SIZES_CLASS = 'admin-sizes'

const DEEPEST_OVERLAY_NESTING = 3

const ADMIN_MENU_PROPS = { contentClass: ADMIN_SIZES_CLASS }

const ADMIN_CONTROL_DEFAULTS: VuetifyDefaults = {
  VAlert: { density: 'compact' },
  VAutocomplete: { density: 'compact', menuProps: ADMIN_MENU_PROPS },
  VBtn: { size: 'default', density: 'comfortable' },
  VBtnToggle: { density: 'compact' },
  VCheckbox: { density: 'compact' },
  VChip: { size: 'small' },
  VCombobox: { density: 'compact', menuProps: ADMIN_MENU_PROPS },
  VList: { density: 'compact' },
  VListItem: { density: 'compact' },
  VNumberInput: { density: 'compact' },
  VRadioGroup: { density: 'compact' },
  VSelect: { density: 'compact', menuProps: ADMIN_MENU_PROPS },
  VSwitch: { density: 'compact' },
  VTable: { density: 'compact' },
  VTabs: { density: 'compact' },
  VTextarea: { density: 'compact' },
  VTextField: { density: 'compact' },
  VToolbar: { density: 'compact' },
  VTooltip: { contentClass: ADMIN_SIZES_CLASS },
}

function overlayDefaults(nestingLeft: number): VuetifyDefaults {
  return nestingLeft === 0
    ? ADMIN_CONTROL_DEFAULTS
    : {
        ...ADMIN_CONTROL_DEFAULTS,
        VDialog: { contentClass: ADMIN_SIZES_CLASS, ...overlayDefaults(nestingLeft - 1) },
        VMenu: { contentClass: ADMIN_SIZES_CLASS, ...overlayDefaults(nestingLeft - 1) },
      }
}

export const ADMIN_DEFAULTS: VuetifyDefaults = overlayDefaults(DEEPEST_OVERLAY_NESTING)
