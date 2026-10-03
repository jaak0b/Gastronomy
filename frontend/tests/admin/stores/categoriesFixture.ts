import { stubLaptopAnswering } from '../../support/laptop'

export const DRINKS = {
  categoryId: '11111111-1111-1111-1111-111111111111',
  name: 'Getränke',
  colourHex: '#C62828',
  sortOrder: 1,
  isActive: true,
}

export const FOOD = {
  categoryId: '22222222-2222-2222-2222-222222222222',
  name: 'Speisen',
  colourHex: '#6D4C41',
  sortOrder: 2,
  isActive: true,
}

export const CREATED_CATEGORY = {
  categoryId: 'category-neu',
  name: 'Getränke',
  colourHex: '#C62828',
  sortOrder: 1,
  isActive: true,
}

export function theTwoCategories() {
  return stubLaptopAnswering(() => ({ categories: [DRINKS, FOOD] }))
}
