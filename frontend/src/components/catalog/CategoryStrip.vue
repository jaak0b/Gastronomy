<script setup lang="ts">
defineProps<{ categories: { name: string; sortOrder: number }[] }>()
defineEmits<{ select: [name: string] }>()
</script>

<template>
  <div class="category-strip d-flex flex-wrap ga-2 my-2">
    <v-chip
      v-for="category in categories"
      :key="category.name"
      :value="category.name"
      @click="$emit('select', category.name)"
    >
      {{ category.name }}
    </v-chip>
  </div>
</template>
