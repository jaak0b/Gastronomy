import type { ThemeDefinition } from 'vuetify'
import type { ShareState } from '../api/generatedSchemas'
import { appTheme } from '../theme'
import { statusTintOf, statusTokenOf } from './statusTints'
import { themeColourOf } from './themeColours'

export interface TablePlanColours {
  drawing: string
  ink: string
  table: string
  station: string
  stationInk: string
  handOutFill: Record<ShareState, string>
  settlementBorder: Record<ShareState, string>
}

function tablePlanColoursOf(definition: ThemeDefinition): TablePlanColours {
  return {
    drawing: themeColourOf(definition, 'surface'),
    ink: themeColourOf(definition, 'on-surface'),
    table: themeColourOf(definition, 'surface-light'),
    station: themeColourOf(definition, 'primary'),
    stationInk: themeColourOf(definition, 'on-primary'),
    handOutFill: {
      none: statusTintOf(definition, statusTokenOf('none')),
      some: statusTintOf(definition, statusTokenOf('some')),
      all: statusTintOf(definition, statusTokenOf('all')),
    },
    settlementBorder: {
      none: themeColourOf(definition, statusTokenOf('none')),
      some: themeColourOf(definition, statusTokenOf('some')),
      all: themeColourOf(definition, statusTokenOf('all')),
    },
  }
}

export const LIGHT_TABLE_PLAN_COLOURS = tablePlanColoursOf(appTheme.themes.light)

export const DARK_TABLE_PLAN_COLOURS = tablePlanColoursOf(appTheme.themes.dark)
