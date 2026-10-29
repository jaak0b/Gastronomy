import type { createVuetify } from 'vuetify'
import { VBtn } from 'vuetify/components'

type VuetifyOptions = NonNullable<Parameters<typeof createVuetify>[0]>

export type VuetifyDefaults = NonNullable<VuetifyOptions['defaults']>

declare module 'vue' {
  export interface GlobalComponents {
    VBtnWide: typeof VBtn
  }
}

const DEEPEST_OVERLAY_NESTING = 3

const APP_DEFAULTS: VuetifyDefaults = {
  VBtn: { variant: 'flat' },
  VTextField: { variant: 'outlined', hideDetails: 'auto' },
  VNumberInput: { variant: 'outlined', hideDetails: 'auto' },
  VSelect: { variant: 'outlined', hideDetails: 'auto' },
  VCheckbox: { hideDetails: 'auto' },
}

export const APP_VUETIFY_OPTIONS: VuetifyOptions = {
  aliases: { VBtnWide: VBtn },
  defaults: APP_DEFAULTS,
}

function withOverlays(controlDefaults: VuetifyDefaults, sizesClass: string, nestingLeft: number): VuetifyDefaults {
  if (nestingLeft === 0) {
    return controlDefaults
  }
  const insideAnOverlay = withOverlays(controlDefaults, sizesClass, nestingLeft - 1)
  return {
    ...controlDefaults,
    VBottomSheet: { contentClass: sizesClass },
    VDialog: { contentClass: sizesClass, ...insideAnOverlay },
    VMenu: { contentClass: sizesClass, ...insideAnOverlay },
  }
}

export function surfaceDefaults(sizesClass: string, controlDefaults: VuetifyDefaults): VuetifyDefaults {
  const menuProps = { contentClass: sizesClass }
  const controlsWithTheirMenus: VuetifyDefaults = {
    ...controlDefaults,
    VAutocomplete: { ...controlDefaults.VAutocomplete, menuProps },
    VCombobox: { ...controlDefaults.VCombobox, menuProps },
    VSelect: { ...controlDefaults.VSelect, menuProps },
    VTooltip: { contentClass: sizesClass },
  }
  return withOverlays(controlsWithTheirMenus, sizesClass, DEEPEST_OVERLAY_NESTING)
}
