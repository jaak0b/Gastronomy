<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AdminFestivalIngredientView,
  AdminFestivalView,
} from '../../../shared/api/generatedSchemas'
import { formatTimeOfDay, spansMoreThanOneDay } from '../../../shared/core/moments'
import { entryUnitsFor, type ParsedAmountInput } from '../../core/ingredientAmounts'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminFestivalStockStore } from '../../stores/festivalStock'
import { useIngredientAmountText } from '../../composables/useIngredientAmountText'
import { useRowRefusals } from '../../composables/useRowRefusals'
import IngredientAmountField from '../ingredients/IngredientAmountField.vue'
import FestivalStockChart from './FestivalStockChart.vue'

const STOCK_INVALID_KEY = 'errors.admin.ingredients.stockInvalid'

const props = defineProps<{ festival: AdminFestivalView }>()

const { t, locale } = useI18n()
const stock = useAdminFestivalStockStore()
const amountText = useIngredientAmountText()
const ingredientRefusals = useRowRefusals()
const ingredientIdShowingHistory = ref<string | null>(null)

const rows = computed(() =>
  stock.ingredients.map((ingredient) => ({ ingredient, warningText: stockWarningTextOf(ingredient) })),
)

async function saveAvailableAmount(
  ingredientId: string,
  availableAmount: number | null,
): Promise<void> {
  ingredientRefusals.showRefusalOf(
    ingredientId,
    await stock.setAvailableAmount(props.festival.festivalId, ingredientId, availableAmount),
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

function stockWarningTextOf(ingredient: AdminFestivalIngredientView): string | null {
  switch (ingredient.stockWarning) {
    case 'none':
      return null
    case 'runsOutSoon':
      if (ingredient.runsOutSoonAtUtc === null) {
        throw new Error(`The laptop warned that ${ingredient.name} runs out soon without naming when.`)
      }
      return t('admin.ingredients.labels.runsOutAt', {
        moment: formatTimeOfDay(
          new Date(ingredient.runsOutSoonAtUtc),
          locale.value,
          spansMoreThanOneDay(props.festival.startsAtUtc, props.festival.endsAtUtc),
        ),
      })
    case 'usedUp':
      return t('admin.ingredients.labels.usedUp')
    default:
      return assertNever(ingredient.stockWarning)
  }
}

function historyToggleIconOf(ingredientId: string): string {
  return ingredientIdShowingHistory.value === ingredientId ? 'mdi-chevron-up' : 'mdi-chevron-down'
}

function toggleHistory(ingredientId: string): void {
  ingredientIdShowingHistory.value = ingredientIdShowingHistory.value === ingredientId ? null : ingredientId
}
</script>

<template>
  <section v-if="stock.ingredients.length > 0" class="festival-stock mb-4" data-test="festival-stock">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3" data-test="section-heading">{{ t('admin.ingredients.title') }}</h2>
        <div
          v-for="({ ingredient, warningText }, position) in rows"
          :key="ingredient.ingredientId"
          class="festival-ingredient-row"
          data-test="festival-ingredient-row"
          :data-test-id="ingredient.ingredientId"
          :class="{ 'tinted-row': position % 2 === 1 }"
        >
          <div class="row-line py-2 px-3">
            <span
              class="name text-body-1"
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
            <span
              class="stock-warning text-body-2 d-flex align-center ga-1"
              data-test="stock-warning"
            >
              <template v-if="warningText !== null">
                <v-icon
                  icon="mdi-alert"
                  color="warning"
                  size="small"
                />
                <span
                  class="text-warning"
                  data-test="stock-warning-text"
                >
                  {{ warningText }}
                </span>
              </template>
            </span>
            <v-btn
              class="history-toggle"
              data-test="history-toggle"
              variant="text"
              size="small"
              :append-icon="historyToggleIconOf(ingredient.ingredientId)"
              @click="toggleHistory(ingredient.ingredientId)"
            >
              {{ t('admin.ingredients.actions.history') }}
            </v-btn>
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
          <template v-if="ingredientIdShowingHistory === ingredient.ingredientId">
            <p
              v-if="ingredient.usedAmount === 0"
              class="nothing-used-yet text-body-2 text-medium-emphasis px-3 pb-3 mb-0"
              data-test="nothing-used-yet"
            >
              {{ t('admin.ingredients.history.nothingUsedYet') }}
            </p>
            <FestivalStockChart
              v-else
              class="px-3 pb-3"
              :ingredient="ingredient"
              :festival="festival"
            />
          </template>
        </div>
      </div>
    </v-card>
  </section>
</template>

<style scoped>
.row-line {
  display: grid;
  grid-template-columns: minmax(8rem, 1fr) 16rem 10rem 17rem 7rem;
  align-items: center;
  column-gap: 0.75rem;
}
</style>
