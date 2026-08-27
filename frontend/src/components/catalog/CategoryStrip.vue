<script setup lang="ts">
defineProps<{ categories: { name: string; sortOrder: number }[]; selected: string | null }>()
defineEmits<{ select: [name: string] }>()
</script>

<template>
  <nav class="category-strip">
    <button
      v-for="category in categories"
      :key="category.name"
      type="button"
      :class="{ 'is-selected': category.name === selected }"
      @click="$emit('select', category.name)"
    >
      {{ category.name }}
    </button>
  </nav>
</template>
