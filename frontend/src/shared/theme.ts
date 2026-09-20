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
    primary: '#e6e8eb',
    together: '#4f8cff',
    individual: '#f57c00',
    success: '#4caf7d',
    warning: '#ffd54f',
    error: '#e06c6c',
    'on-primary': '#1b1e24',
    'on-together': '#ffffff',
    'on-individual': '#1b1e24',
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
    primary: '#1f2937',
    together: '#2f6fdb',
    individual: '#ef6c00',
    success: '#2e8b57',
    warning: '#f9a825',
    error: '#c94f4f',
    'on-primary': '#ffffff',
    'on-together': '#ffffff',
    'on-individual': '#1f2328',
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
} as const
