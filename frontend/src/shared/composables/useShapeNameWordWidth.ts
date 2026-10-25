import Konva from 'konva'
import { onUnmounted } from 'vue'
import { LARGEST_NAME_FONT_SIZE } from '../core/shapeNameFit'

export function useShapeNameWordWidth(): (word: string) => number {
  const wordMeasure = new Konva.Text({ fontSize: LARGEST_NAME_FONT_SIZE })
  onUnmounted(() => wordMeasure.destroy())
  return (word) => wordMeasure.measureSize(word).width
}
