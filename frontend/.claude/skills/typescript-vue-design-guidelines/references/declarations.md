# Declaration Do's and Don'ts

Condensed from the "Do's and Don'ts" page of the TypeScript handbook's Declaration Files section.
The page is written for `.d.ts` files, and every rule applies just as well to an exported signature
in a `.ts` file or an interface in `apiTypes.ts`.

## General types

### Number, String, Boolean, Symbol and Object

- **Don't** ever use the types `Number`, `String`, `Boolean`, `Symbol` or `Object`. They refer to
  the boxed objects, which are almost never what JavaScript code means.
- **Do** use `number`, `string`, `boolean` and `symbol`.
- **Do** use the non-primitive `object` type instead of `Object`.

```ts
function reverse(s: string): string
```

```ts
function reverse(s: String): String
```

### Generics

- **Don't** ever declare a generic type that does not use its type parameter. The parameter then
  constrains nothing, and every call site is free to pick any type argument without the compiler
  noticing a mismatch.

### any

- **Don't** use `any` as a type unless you are in the middle of migrating a JavaScript project. The
  compiler treats `any` as "turn off type checking for this thing", which is the same as an
  `@ts-ignore` on every usage.
- **Do** use `unknown` when you do not know which type to accept, or when the value is passed
  through without being touched.

## Callback types

### Return types of callbacks

- **Don't** type a callback whose result is ignored as returning `any`.
- **Do** type it as returning `void`. `void` stops the caller from using the return value by
  accident, which `any` would allow unchecked.

```ts
function fn(x: () => void) {
  x()
}
```

```ts
function fn(x: () => any) {
  x()
}
```

With `void`, `const k = x(); k.doSomething()` is an error; with `any` it compiles and fails at
runtime.

### Optional parameters in callbacks

- **Don't** mark a callback's parameter optional unless the callback really is sometimes invoked
  with fewer arguments. `(data: unknown, elapsedTime?: number) => void` says the callback may be
  called with one argument or with two. A callback that ignores a parameter is always legal, so
  there is no need for the optional marker to allow that.
- **Do** write callback parameters as non-optional.

```ts
interface Fetcher {
  getObject(done: (data: unknown, elapsedTime: number) => void): void
}
```

```ts
interface Fetcher {
  getObject(done: (data: unknown, elapsedTime?: number) => void): void
}
```

### Overloads and callbacks

- **Don't** write separate overloads that differ only in the arity of a callback parameter.
- **Do** write one signature using the maximum arity. A callback may always disregard a parameter,
  so the shorter overload adds nothing, and listing it first lets a wrongly typed function through
  because it matches the first overload.

```ts
function beforeAll(action: (done: DoneFn) => void, timeout?: number): void
```

```ts
function beforeAll(action: () => void, timeout?: number): void
function beforeAll(action: (done: DoneFn) => void, timeout?: number): void
```

## Function overloads

### Ordering

- **Don't** put a more general overload before a more specific one. TypeScript picks the first
  matching overload, so a general signature listed first hides every later one:
  `fn(myDivElement)` resolves to `unknown` instead of `string`.
- **Do** sort overloads so the more general signatures come after the more specific ones.

```ts
function fn(x: HTMLDivElement): string
function fn(x: HTMLElement): number
function fn(x: unknown): unknown
```

```ts
function fn(x: unknown): unknown
function fn(x: HTMLElement): number
function fn(x: HTMLDivElement): string
```

### Use optional parameters

- **Don't** write several overloads that differ only in trailing parameters.
- **Do** use optional parameters whenever possible. This collapsing is only correct when all the
  overloads share one return type.

```ts
interface Example {
  diff(one: string, two?: string, three?: boolean): number
}
```

```ts
interface Example {
  diff(one: string): number
  diff(one: string, two: string): number
  diff(one: string, two: string, three: boolean): number
}
```

Two reasons. Signature compatibility is checked by asking whether any signature of the target can
be invoked with the source's arguments, and extra arguments are allowed, so passing `x.diff` to a
function expecting `(a: string, b: number, c: number) => void` is wrongly accepted with the
overloads and correctly rejected with the optional parameters. And under strict null checks an
explicit `undefined` is fine for an optional parameter (`x.diff('something', cond ? undefined :
'hour')`), but an error against the overload that demands a `string`.

### Use union types

- **Don't** write overloads that differ by type in only one argument position.
- **Do** use a union type for that parameter whenever possible.

```ts
interface Moment {
  utcOffset(): number
  utcOffset(b: number | string): Moment
}
```

```ts
interface Moment {
  utcOffset(): number
  utcOffset(b: number): Moment
  utcOffset(b: string): Moment
}
```

`b` is not made optional here because the two return types differ. The union matters for code
that passes a value through: a function taking `x: number | string` can call `utcOffset(x)` only
when the parameter is a union, since separate overloads reject the union argument.
