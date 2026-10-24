import type { ThemeDefinition } from 'vuetify'
import { appTheme } from '../theme'
import { mutedInkOf, themeColourOf } from './themeColours'

export interface TablePlanColours {
  drawing: string
  ink: string
  muted: string
  table: string
  station: string
  stationInk: string
}

function tablePlanColoursOf(definition: ThemeDefinition): TablePlanColours {
  return {
    drawing: themeColourOf(definition, 'surface'),
    ink: themeColourOf(definition, 'on-surface'),
    muted: mutedInkOf(definition),
    table: themeColourOf(definition, 'surface-light'),
    station: themeColourOf(definition, 'primary'),
    stationInk: themeColourOf(definition, 'on-primary'),
  }
}

export const LIGHT_TABLE_PLAN_COLOURS = tablePlanColoursOf(appTheme.themes.light)

export const DARK_TABLE_PLAN_COLOURS = tablePlanColoursOf(appTheme.themes.dark)
