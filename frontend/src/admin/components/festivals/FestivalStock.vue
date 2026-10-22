<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { formatRunOutMoment } from '../../../shared/core/moments'
import { entryUnitsFor, type ParsedAmountInput } from '../../core/ingredientAmounts'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminFestivalStockStore } from '../../stores/festivalStock'
import { useIngredientAmountText } from '../../composables/useIngredientAmountText'
import { useRowRefusals } from '../../composables/useRowRefusals'
import IngredientAmountField from '../ingredients/IngredientAmountField.vue'

const STOCK_INVALID_KEY = 'errors.admin.ingredients.stockInvalid'

const props = defineProps<{ festivalId: string }>()

const { t, locale } = useI18n()
const stock = useAdminFestivalStockStore()
const amountText = useIngredientAmountText()
const ingredientRefusals = useRowRefusals()

async function saveAvailableAmount(
  ingredientId: string,
  availableAmount: number | null,
): Promise<void> {
  ingredientRefusals.showRefusalOf(
    ingredientId,
    await stock.setAvailableAmount(props.festivalId, ingredientId, availableAmount),
  )
}

async function changeAvailableAmount(
  ingredientId: string,
  parsed: ParsedAmountInput,
): Promise<void> {
  ingredientRefusals.forgetRefusalOf(ingredientId)
  switch (parsed.kind) {
    case 'amount':
      await saveAvailableAmount(ingredientId, parsed.baseAmount)
      return
    case 'empty':
      await saveAvailableAmount(ingredientId, null)
      return
    case 'unreadable':
      ingredientRefusals.refuse(ingredientId, STOCK_INVALID_KEY)
      return
    default:
      assertNever(parsed)
  }
}

function runOutText(runsOutAtUtc: string | null): string {
  if (runsOutAtUtc === null) {
    return ''
  }
  return t('admin.ingredients.labels.runsOutAt', {
    moment: formatRunOutMoment(runsOutAtUtc, locale.value, new Date()),
  })
}
</script>

<template>
  <section v-if="stock.ingredients.length > 0" class="festival-stock mb-4" data-test="festival-stock">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3" data-test="section-heading">{{ t('admin.ingredients.title') }}</h2>
        <div
          v-for="(ingredient, position) in stock.ingredients"
          :key="ingredient.ingredientId"
          class="festival-ingredient-row"
          data-test="festival-ingredient-row"
          :data-test-id="ingredient.ingredientId"
          :class="{ 'tinted-row': position % 2 === 1 }"
        >
          <div class="row-line d-flex align-center flex-wrap ga-4 py-2 px-3">
            <span
              class="name text-body-1 flex-grow-1"
              data-test="ingredient-name"
            >
              {{ ingredient.name }}
            </span>
            <IngredientAmountField
              :entry-units="entryUnitsFor(ingredient.unit)"
              :saved-amount="ingredient.availableAmount"
              :label="t('admin.ingredients.labels.available')"
              @amount-typed="ingredientRefusals.forgetRefusalOf(ingredient.ingredientId)"
              @amount-changed="changeAvailableAmount(ingredient.ingredientId, $event)"
            />
            <span
              class="used-amount text-body-2"
              data-test="used-amount"
            >
              {{
                t('admin.ingredients.labels.used', {
                  amount: amountText(ingredient.usedAmount, ingredient.unit),
                })
              }}
            </span>
            <span class="runs-out text-body-2" data-test="runs-out">{{ runOutText(ingredient.runsOutAtUtc) }}</span>
          </div>
          <v-alert
            v-if="ingredientRefusals.refusalTextOf(ingredient.ingredientId) !== null"
            class="refusal mb-2"
            data-test="refusal"
            type="warning"
            variant="tonal"
          >
            {{ ingredientRefusals.refusalTextOf(ingredient.ingredientId) }}
          </v-alert>
        </div>
      </div>
    </v-card>
  </section>
</template>
