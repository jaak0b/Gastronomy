import { surfaceDefaults, type VuetifyDefaults } from '../../shared/styles/appVuetify'

export const STATION_SIZES_CLASS = 'station-sizes'

const STATION_CONTROL_DEFAULTS: VuetifyDefaults = {
  VBtn: { size: 'large' },
  VChip: { size: 'large' },
  VSelect: { density: 'comfortable' },
}

export const STATION_DEFAULTS: VuetifyDefaults = surfaceDefaults(STATION_SIZES_CLASS, STATION_CONTROL_DEFAULTS)
