<script setup lang="ts">
import { computed } from 'vue'
import { assertNever } from '../core/assertNever'

type DialogButtonIntent = 'primary' | 'secondary' | 'quiet'

type DialogButtonStyle = {
  variant: 'flat' | 'tonal' | 'outlined'
  color: string | undefined
}

const props = defineProps<{ intent: DialogButtonIntent }>()

const buttonStyle = computed<DialogButtonStyle>(() => {
  switch (props.intent) {
    case 'primary':
      return { variant: 'flat', color: 'primary' }
    case 'secondary':
      return { variant: 'tonal', color: 'primary' }
    case 'quiet':
      return { variant: 'outlined', color: undefined }
    default:
      return assertNever(props.intent)
  }
})
</script>

<template>
  <v-btn :variant="buttonStyle.variant" :color="buttonStyle.color">
    <slot />
  </v-btn>
</template>
