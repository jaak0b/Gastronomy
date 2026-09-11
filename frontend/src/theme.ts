import type { ThemeDefinition } from 'vuetify'

const dark: ThemeDefinition = {
  dark: true,
  colors: {
    background: '#1b1e24',
    surface: '#252a32',
    'surface-bright': '#2e343d',
    'surface-light': '#2e343d',
    'surface-variant': '#343a44',
    'on-background': '#e6e8eb',
    'on-surface': '#e6e8eb',
    'on-surface-variant': '#e6e8eb',
    primary: '#4f8cff',
    warning: '#e0a03a',
    success: '#4caf7d',
    error: '#e06c6c',
  },
  variables: {
    'border-color': '#343a44',
    'border-opacity': 1,
    'medium-emphasis-opacity': 0.66,
  },
}

const light: ThemeDefinition = {
  dark: false,
  colors: {
    background: '#f4f5f7',
    surface: '#ffffff',
    'surface-bright': '#ffffff',
    'surface-light': '#ebedf0',
    'surface-variant': '#59626d',
    'on-background': '#1f2328',
    'on-surface': '#1f2328',
    'on-surface-variant': '#ffffff',
    primary: '#2f6fdb',
    warning: '#b7791f',
    success: '#2e8b57',
    error: '#c94f4f',
  },
  variables: {
    'border-color': '#e1e4e8',
    'border-opacity': 1,
    'medium-emphasis-opacity': 0.71,
  },
}

export const appTheme = {
  defaultTheme: 'system',
  themes: { light, dark },
}
