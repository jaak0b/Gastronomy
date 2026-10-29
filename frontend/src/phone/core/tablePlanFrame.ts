interface RectangleSize {
  width: number
  height: number
}

interface RectangleToStroke {
  x: number
  y: number
  width: number
  height: number
}

export function rectangleToStrokeInside(size: RectangleSize, strokeWidth: number): RectangleToStroke {
  const halfOfTheStroke = strokeWidth / 2
  return {
    x: halfOfTheStroke,
    y: halfOfTheStroke,
    width: size.width - strokeWidth,
    height: size.height - strokeWidth,
  }
}
