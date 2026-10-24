import { ref } from 'vue'
import {
  laptopConfirmedField,
  stillDiffersFromTheLaptop,
  type LaptopConfirmedField,
} from '../core/laptopConfirmedField'

export interface TypedNames {
  nameShownFor: (rowId: string, savedName: string) => string
  hasTypedName: (rowId: string) => boolean
  type: (rowId: string, savedName: string, typed: string) => void
  takeNameToSend: (rowId: string, savedName: string) => string | null
  forgetTypedName: (rowId: string) => void
}

export function useTypedNames(): TypedNames {
  const typedNames = ref(new Map<string, LaptopConfirmedField<string>>())

  function nameShownFor(rowId: string, savedName: string): string {
    return typedNames.value.get(rowId)?.edited ?? savedName
  }

  function hasTypedName(rowId: string): boolean {
    return typedNames.value.has(rowId)
  }

  function type(rowId: string, savedName: string, typed: string): void {
    const field = typedNames.value.get(rowId)
    if (field === undefined) {
      typedNames.value.set(rowId, laptopConfirmedField(typed, savedName))
      return
    }
    field.edited = typed
  }

  function takeNameToSend(rowId: string, savedName: string): string | null {
    const field = typedNames.value.get(rowId)
    if (field === undefined || !stillDiffersFromTheLaptop(field)) {
      return null
    }
    if (field.edited === savedName) {
      typedNames.value.delete(rowId)
      return null
    }
    field.confirmedByTheLaptop = field.edited
    return field.edited
  }

  function forgetTypedName(rowId: string): void {
    typedNames.value.delete(rowId)
  }

  return { nameShownFor, hasTypedName, type, takeNameToSend, forgetTypedName }
}
