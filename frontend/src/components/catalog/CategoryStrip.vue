<script setup lang="ts">
defineProps<{ categories: { name: string; sortOrder: number }[]; selected: string | null }>()
defineEmits<{ select: [name: string] }>()
</script>

<template>
  <v-chip-group class="category-strip" :model-value="selected" column>
    <v-chip
      v-for="category in categories"
      :key="category.name"
      :value="category.name"
      :class="{ 'is-selected': category.name === selected }"
      filter
      @click="$emit('select', category.name)"
    >
      {{ category.name }}
    </v-chip>
  </v-chip-group>
</template>
