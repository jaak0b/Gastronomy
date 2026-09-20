# TypeScript guidelines

Condensed from the Google TypeScript Style Guide: source files, modules, language features,
naming, type system, toolchain and policies. The guide's comment and JSDoc chapters are replaced by
this repository's rule that a name carries what a comment would have said. Examples use this
codebase's forms: no semicolons, single quotes, `<script setup lang="ts">`, Pinia stores and
Vitest tests.

The source uses RFC 2119 words: must, must not, should, should not, may. "Prefer" and "avoid"
mean should and should not. Examples in the source are illustrations, and a formatting choice made
in an example is not a rule.

## Source files

### Encoding and escapes

- Source files are UTF-8, and the only whitespace character besides the line terminator is the
  ASCII space. Every other whitespace character inside a string literal is escaped.
- A character with a special escape sequence (`\'`, `\"`, `\\`, `\b`, `\f`, `\n`, `\r`, `\t`,
  `\v`) uses that sequence, never the numeric escape. Legacy octal escapes are never used.
- A printable non-ASCII character is written as itself (`const units = 'μs'`), never as
  `'μs'`. A non-printable character uses its hex or Unicode escape, and the name of the
  constant says what it is: `const BYTE_ORDER_MARK = '﻿'`.

### File structure

A file consists of, in order: imports if present, then the implementation. Exactly one blank line
separates the two. (The source also allows a copyright block and a `@fileoverview` JSDoc at the
top; neither is written here.)

### Imports

| Form | Example | Use for |
|---|---|---|
| Named | `import { StationEstimate } from '../core/apiTypes'` | The usual case |
| Namespace | `import * as tableview from './tableview'` | Many symbols from a large module |
| Default | `import Button from 'Button'` | Only external code that requires it |
| Side effect | `import 'jasmine'` | Only a library imported for what it does on load |

- Code must import other TypeScript code by path. Use a relative path (`./foo`, `../core/foo`)
  for files inside the same project, so the project can move without touching its imports.
  Consider limiting the number of parent steps (`../../../`), which make module structure hard to
  follow.
- Prefer named imports for symbols used often in a file or whose names are clear on their own, such
  as Vitest's `describe` and `it`. A named import can be aliased with `as`.
- Prefer a namespace import when using many different symbols from a large module, or when the
  exported names are common words such as `Model` or `Controller` that would otherwise need aliases.
  A namespace import gives a name to the module's exports; it is not a wildcard import.

```ts
import * as tableview from './tableview'

let item: tableview.Item | undefined
```

```ts
import { describe, expect, it } from 'vitest'

describe('parseEuroInput', () => {
  it('reads a comma as the decimal separator', () => {
    expect(parseEuroInput('3,50')).toBe(350)
  })
})
```

- Fix a name collision with a namespace import or by renaming the export itself. Renaming an
  import (`import { SomeThing as SomeOtherThing }`) is allowed when needed: to avoid a collision, when
  the imported name is generated, or when the imported name is unclear on its own.

### Exports

- Use named exports in all code. Do not use default exports. A default export has no canonical
  name, so two files can import it under two names, and a misspelled named import fails at compile
  time while a default import silently binds to whatever the module exported.

```ts
export function parseEuroInput(typed: string): number | null { ... }
```

```ts
export default function parseEuroInput(typed: string): number | null { ... }
```

- Only export symbols used outside the module, and keep the exported surface small. TypeScript
  cannot restrict the visibility of an export any further.
- Do not export a mutable binding: `export let` is not allowed. A re-export across files does not
  see the change. Expose a getter function instead, and make every export final once the module
  body has run: do the conditional check first, then export the chosen value as a `const`.

```ts
let attemptCount = 0
export function attemptsSoFar(): number {
  return attemptCount
}
```

- Do not create a class of static members for the sake of namespacing. Export the constants and
  functions themselves; the file is already a scope.

```ts
export const UNAUTHORISED = 401
export function answerSaysTheDeviceIsNoLongerSetUp(result: ApiResult<unknown>): boolean { ... }
```

### Import and export type

- Use `import type { Foo }` or `import { type Foo, Bar }` for a symbol used only as a type, and a
  regular import for a value.
- Use `export type { AnInterface } from './foo'` when re-exporting a type, so file-by-file
  transpilation works. `export type` does not guarantee that an API is used only as a type;
  splitting the symbols into two (`UserService` and `AjaxUserService`) does.

### Modules, not namespaces

- Code must not use `namespace Foo { ... }` or the older `module Foo { ... }`. Refer to code in
  other files through `import` and `export` only; to namespace code semantically, use separate
  files. A namespace is allowed only when an external library requires one.
- Code must not use `import x = require('...')` or `/// <reference path="..."/>`.

## Language features

A feature the guide does not mention may be used freely.

### Variables

- Always declare with `const` or `let`. Use `const` by default and `let` only when the variable is
  reassigned. Never use `var`: it is function scoped and causes hard-to-see bugs.
- A variable must not be used before its declaration.
- Every declaration declares one variable; `let a = 1, b = 2` is not used.

### Array literals

- Do not use the `Array()` constructor, with or without `new`. `new Array(2)` gives two empty
  slots and `new Array(2, 3)` gives `[2, 3]`. Use bracket notation, or `Array.from` for a sized
  array: `Array.from<number>({ length: 5 }).fill(0)`.
- Do not define or read non-numeric properties on an array, other than `length`. Use a `Map` or an
  object.
- Spread syntax `[...foo]` shallow-copies or concatenates an iterable. Only spread an iterable into
  an array; never spread a primitive, `null` or `undefined`. `[5, ...(condition && foo)]` may
  spread `undefined`; write `const extra = condition ? [7] : []` first.
- Array destructuring may unpack several values, and a final rest element is allowed. Omit unused
  elements: `const [a, , c] = values`. A destructured array parameter that is optional defaults to
  `[]`, with element defaults on the left side: `function f([a = 4, b = 2] = [])`, never
  `function f([a, b] = [4, 2])`. For unpacking several values, prefer object destructuring, which
  names each element and types each one separately.

### Object literals

- Do not use the `Object` constructor. Use a literal: `{}` or `{ a: 0, b: 1 }`.
- Do not iterate an object with an unfiltered `for (... in ...)`; it walks the prototype chain.
  Filter with `hasOwnProperty`, or prefer `for (const key of Object.keys(obj))`,
  `Object.values(obj)` or `Object.entries(obj)`.
- Spread syntax `{ ...bar }` shallow-copies an object, and a later key replaces an earlier one.
  Only spread objects into an object; never an array, a primitive, `null` or `undefined`. Avoid
  spreading an instance of a class or a function: only enumerable own properties are copied.
- A computed property name (`{ ['key' + foo()]: 42 }`) is allowed and counts as a quoted key, so
  it must not be mixed with unquoted keys, unless the computed key is a symbol.
- Object destructuring may unpack values on the left of an assignment. As a function parameter it
  stays simple: one level of unquoted shorthand properties, no nesting, no computed keys. Defaults
  go on the left side of the pattern, and an optional destructured object defaults to `{}`.

```ts
interface Options {
  count?: number
  label?: string
}

function destructured({ count, label = 'default' }: Options = {}) {}
```

```ts
function nestedTooDeeply({ x: { count, label } }: { x: Options }) {}
function nontrivialDefault({ count, label }: Options = { count: 42, label: 'default' }) {}
```

### Classes

- A class declaration is not terminated with a semicolon; a statement containing a class expression
  is (`export const Baz = class extends Bar { ... };`, where the file uses semicolons).
- Method declarations are not separated by semicolons, and one blank line separates them from the
  surrounding code. Blank lines directly inside the class braces are neither required nor
  forbidden.
- `toString` may be overridden, but must always succeed and must have no visible side effect.
  Calling other methods from it risks an infinite loop.

**Static members**

- Prefer a module-local function over a private static method where readability allows it.
- Do not rely on dynamic dispatch of static methods. Call a static method on the class that defines
  it, never on a variable holding a constructor or on a subclass that does not define it.
- Code must not use `this` in a static context. Static fields are inherited and can be overridden
  through `this`, which surprises readers and encourages static state that hurts testability.

**Constructors**

- A constructor call uses parentheses even without arguments: `new Foo()`, never `new Foo`.
  `new Foo().Bar()` and `new Foo.Bar()` are different calls.
- Do not write an empty constructor or one that only calls `super` with the same parameters.
  Keep a constructor that has parameter properties, visibility modifiers or parameter decorators,
  even with an empty body, and a `private constructor() {}` that forbids instantiation.
- One blank line separates the constructor from the members above and below.

**Class members**

- Do not use `#private` fields. Use the `private` modifier: they cost emit size and speed when
  down-leveled, and static type checking already enforces visibility.
- Mark a property that is never reassigned outside the constructor `readonly`. It need not be
  deeply immutable.
- Use a parameter property rather than declaring a field and assigning a constructor parameter to it.

```ts
class OrderSender {
  constructor(private readonly client: ApiClient) {}
}
```

```ts
class OrderSender {
  private readonly client: ApiClient

  constructor(client: ApiClient) {
    this.client = client
  }
}
```

- Initialize a member that is not a parameter where it is declared, which often removes the
  constructor: `private readonly userList: string[] = []`.
- Never add or remove properties on an instance after the constructor has finished. An optional
  field filled in later is initialized to `undefined` explicitly.
- A property used outside the class's lexical scope, such as a component property read from a
  template, must not be `private`; use `protected` or public as fits.
- Code must not use `obj['foo']` to bypass visibility. A private property declares to tools and
  readers that only the class touches it, and an unused-code check will rely on that.

**Getters and setters**

- Accessors may be used. A getter must be a pure function: consistent result, no side effect, no
  change to observable state. `get next() { return this.nextId++ }` is wrong.
- When an accessor hides a property, the hidden property may be prefixed or suffixed with a whole
  word such as `internal` or `wrapped`, and code reads it through the accessor where possible. At
  least one accessor of the pair must be non-trivial; a pass-through getter and setter pair only
  hides a property, so make the property public, or `readonly`, instead.
- Accessors must not be defined with `Object.defineProperty`.

**Computed properties and visibility**

- A computed property in a class is allowed only when the key is a symbol. A class that is
  logically iterable defines `[Symbol.iterator]`. Use `Symbol` sparingly beyond that.
- Limit the visibility of properties, methods and types as far as possible. Consider turning a
  private method into a non-exported function in the same file, and moving private properties into
  a separate non-exported class.
- Members are public by default. Never write `public`, except on a non-readonly public parameter
  property in a constructor: `constructor(public baz: Baz)`. `public readonly baz` is wrong since
  `readonly` already makes it a property.

**Disallowed class patterns**

- Do not manipulate prototypes directly. Mixins and changes to the prototypes of builtin objects
  are forbidden; only framework code may need prototypes.

### Functions

Terms: a function declaration uses the `function` keyword as a statement; a function expression
uses `function` inside an expression; an arrow function uses `=>`, with a block body (braces) or a
concise body (an expression). Methods and constructors are covered under Classes.

- Prefer a function declaration for a named function. An arrow function is for a value that needs
  an explicit type annotation, such as a function implementing a call-signature interface.

```ts
function basketItemCount(draft: DraftOrder): number { ... }
```

```ts
const basketItemCount = (draft: DraftOrder): number => ...
```

- A function nested in a method or function may be a declaration or an arrow function. Inside a
  method body, prefer the arrow function, which sees the outer `this`.
- Do not use a function expression; use an arrow function. The only exceptions are code that has
  to rebind `this` dynamically (discouraged) and a generator, which has no arrow form.
- Use a concise body only when the return value is actually used; otherwise use a block body, so
  the return type is `void` and nothing leaks.

```ts
promise.then((value) => {
  record(value)
})
```

```ts
promise.then((value) => record(value))
```

- A function expression or declaration must not use `this` unless it exists to rebind it. Prefer
  an arrow function, or an explicit parameter, over `f.bind(this)` or `const self = this`.
- Prefer passing an arrow function that forwards the arguments over passing a named callback,
  unless both signatures are stable. Optional parameters are the usual trap:
  `['11', '5', '10'].map(parseInt)` yields `[11, NaN, 2]` because the index becomes the radix.

```ts
const numbers = ['11', '5', '3'].map((n) => parseInt(n))
```

- A class usually should not have a property initialized to an arrow function. A call site
  `setTimeout(this.tracker, 5000)` looks like it forgot to bind `this`. Call instance methods from
  an arrow function (`setTimeout(() => { this.tracker() }, 5000)`) and do not pass a method
  reference around. The exception is an event handler that has to be uninstalled later: an arrow
  function property captures `this` and gives a stable reference for `removeEventListener`.
- Do not `bind` in the expression that installs an event handler; the bound reference is temporary
  and cannot be uninstalled.
- A default parameter initializer must not have an observable side effect and should stay simple.
  `function newId(index = counter++)` and a default that exposes shared mutable state are wrong.
  Use defaults sparingly; with more than a few optional parameters without a natural order,
  destructure an options object.
- Use a rest parameter, never `arguments`, and never name anything `arguments`. Use spread syntax
  instead of `Function.prototype.apply`.
- No blank line at the start or end of a function body; a single blank line inside may group
  statements. A generator attaches `*` to the keyword: `function* foo()`, `yield* iter`. No space
  after `...` in rest or spread.
- Use `this` only in a class constructor or method, in a function with an explicit `this` type
  (`function f(this: ThisType, ...)`), or in an arrow function inside one of those. Never use it
  for the global object, the `eval` context, the event target, or through an unneeded `call()` or
  `apply()`.

### Primitive literals

- Ordinary string literals use single quotes. A string containing a single quote can be a
  template literal instead of escaping.
- Do not use a line continuation (a backslash at the end of a line inside a string). Concatenate
  shorter strings, or keep one long string when breaking it would hinder search.
- Use a template literal over complex concatenation, especially with several literals. A template
  literal may span lines without following the block's indentation.
- Numbers may be decimal, hex, octal or binary with the lowercase prefixes `0x`, `0o`, `0b`. Never
  a leading zero otherwise.

### Type coercion

- Coerce with `String()`, `Boolean()` (never with `new`), a template literal, or `!!`.
- An enum value, including a union of an enum with other types, must never be converted to a
  boolean with `Boolean()`, `!!` or an implicit condition. Compare it explicitly:
  `level !== SupportLevel.NONE`, and for an optional value
  `level !== undefined && level !== SupportLevel.NONE`. The first declared enum value is `0` and
  therefore falsy, which readers do not expect.
- String concatenation to cast to a string is discouraged; operands of `+` should match in type.
- Parse numbers with `Number()` and check for `NaN` explicitly unless failure is impossible from
  context. `Number('')`, `Number(' ')` and `Number('\t')` give `0`; `Number('Infinity')` and
  overflowing exponents give `Infinity`.
- Do not use unary `+` to parse a string; it is easy to miss in review and parsing at the wrong
  layer is a code smell.
- Do not use `parseInt` or `parseFloat` except for a non-base-ten string. Both ignore trailing
  characters, so `'12 dwarves'` parses as `12`. With a radix, check the input against the digits of
  that radix first. Parse an integer with `Number()` and then `Math.floor` or `Math.trunc`.
- Do not coerce explicitly inside a condition that already coerces: write `if (foo)`, not
  `if (!!foo)`. Other values may be coerced implicitly or compared explicitly:
  `if (arr.length > 0)` and `if (arr.length)` are both fine.

### Control structures

- Every `if`, `else`, `for`, `do` and `while` body is a braced block, even with one statement, and
  the first statement of a non-empty block starts on its own line. Exception: an `if` that fits on
  one line may omit the block: `if (x) x.doFoo()`.
- Prefer not to assign inside a control statement; it is easily mistaken for an equality check.
  Where it is preferred, wrap the assignment in a second pair of parentheses:
  `while ((x = next()))`.
- Prefer `for (... of someArr)` to iterate an array; `forEach` and a counted `for` loop are also
  allowed, the latter when the index is needed (or use `someArr.entries()`).
- `for ... in` may only be used on dict-style objects, never on an array (it gives the indices as
  strings), and with a `hasOwnProperty` filter. Prefer `for ... of` with `Object.keys`,
  `Object.values` or `Object.entries`.
- Omit optional grouping parentheses only when author and reviewer agree that the code cannot be
  misread without them; nobody has the precedence table memorized. Do not parenthesize the whole
  expression after `delete`, `typeof`, `void`, `return`, `throw`, `case`, `in`, `of` or `yield`.

### Exception handling

- Use exceptions whenever exceptional cases occur, and prefer throwing over ad-hoc error passing
  (an error container by reference, an object with an `error` property). Define a custom error
  class wherever the native `Error` carries too little.
- Instantiate with `new Error()`, never `Error()`.
- Only throw, and only reject a promise with, an `Error` or a subclass. A thrown string has no
  stack trace. `Promise.reject('oh noes!')` and `reject('oh noes!')` are equivalent to throwing a
  string.

```ts
throw new Error('Unhandled union member')
class SendFailure extends Error {}
Promise.reject(new Error('oh noes!'))
```

- When catching, assume every thrown value is an `Error`. Catch as `unknown`, narrow with an
  assertion function, then read `message` or rethrow. Do not defend against non-`Error` values
  unless a called API is known to throw them; in that case the narrowing function's name says which
  API does it.

```ts
function assertIsError(caught: unknown): asserts caught is Error {
  if (!(caught instanceof Error)) throw new Error('caught value is not an Error')
}

try {
  doSomething()
} catch (caught: unknown) {
  assertIsError(caught)
  displayError(caught.message)
}
```

- It is very rarely correct to do nothing in a catch block. The source allows an empty catch with a
  comment; this repository does not allow the comment, so an empty catch is never written. Either
  the catch surfaces the error, rethrows it, or returns a value the caller acts on, and the
  function's name states the fallback (`refusalFrom(payload)` that returns a `gone` result when the
  payload does not parse).
- Keep a `try` block to the calls that can throw, so a reader sees which call is guarded. A call
  that does not throw may stay inside when a temporary variable would cost more than it helps. A
  `try` around a whole loop is fine when a `try` inside the loop would cost performance.

```ts
let result
try {
  result = methodThatMayThrow()
} catch (caught: unknown) {
  ...
}
use(result)
```

### Switch statements

- Every `switch` has a `default` group, even an empty one, and it is last.
- Every non-empty case group ends with `break`, `return` or `throw`; a non-empty group must not
  fall through. Empty groups may fall through to share a body.

### Equality

- Always use `===` and `!==`. The double operators coerce in ways that are hard to understand. The
  only exception is `== null` and `!= null`, which cover `null` and `undefined` in one check.

### Type and non-null assertions

- A type assertion (`x as Foo`) and a non-null assertion (`y!`) only silence the compiler and
  insert no runtime check, so they can crash the program. Do not use either without an obvious or
  explicit reason. Prefer a runtime check: `if (x instanceof Foo)`, `if (y)`.
- The source asks for a comment stating why an assertion is safe. Here the reason goes into the
  structure instead: a narrowing function whose name states the guarantee, or a check the code
  performs.
- Assertions use the `as` syntax, never angle brackets, which forces parentheses on member access:
  `(z as Foo).length`.
- A double assertion goes through `unknown`, never through `any` or `{}`:
  `(x as unknown as Foo).fooMethod()`.
- Give an object literal its type with an annotation (`const foo: Foo = {...}`), never with
  `as Foo`, so a renamed field is caught at the literal.

### Decorators

- Do not define new decorators. Only decorators a framework defines may be used; decorators
  diverged from the TC39 proposal and carry known bugs.
- A decorator immediately precedes the symbol it decorates, with no blank line between.

### Disallowed features

- Never instantiate a primitive wrapper: `new String('hello')`, `new Boolean(false)`,
  `new Number(5)`. `new Boolean(false)` is truthy. Calling a wrapper as a function to coerce is fine.
- Do not rely on automatic semicolon insertion where the file uses semicolons; a file that omits
  them matches its neighbours, per the consistency policy below.
- Code must not use `const enum`; use a plain `enum`. Enums cannot be mutated anyway, and
  `const enum` hides the enum from JavaScript users of the module.
- No `debugger` statement in production code.
- No `with`.
- No `eval` and no `Function(...string)`; they do not work under a strict content security policy.
- No non-standard feature: nothing deprecated or removed from ECMAScript or the web platform,
  nothing still in a TC39 draft or proposal, nothing from a WHATWG proposal that has not finished,
  no transpiler-only language extension. Only features in the current ECMA-262 specification.
- Never modify builtin types, neither their constructors nor their prototypes, and avoid libraries
  that do. Do not add symbols to the global object unless a third-party API demands it.

## Naming

### Identifiers

- Identifiers use only ASCII letters, digits, underscores (for constants and structured test names)
  and, rarely, `$`.
- TypeScript carries information in types, so a name must not repeat what the type says: no
  leading or trailing underscore on a private member, no `opt_` prefix on an optional parameter,
  no `I` prefix or `Interface` suffix on an interface. An interface introduced for a class is named
  for why it exists: `class TodoItem` and `interface TodoItemStorage` when the interface is the
  stored JSON shape.
- A `$` suffix on an observable is a known external convention; whether to use it is a project
  decision, applied consistently. Otherwise identifiers should not use `$` except where a
  third-party framework requires it.

### Descriptive names

- Names must be descriptive and clear to a new reader. Do not use an abbreviation that is ambiguous
  or unknown outside the project, and do not abbreviate by deleting letters inside a word.
  Exception: a variable in scope for ten lines or fewer, including a parameter that is not part of
  an exported API, may have a short name.

| Good | Wrong | Why |
|---|---|---|
| `errorCount` | `nErr` | Ambiguous abbreviation |
| `dnsConnectionIndex` | `nCompConns` | Ambiguous abbreviation |
| `referrerUrl` | `wgcConnections` | Only the team knows the term |
| `customerId` | `cstmrId` | Letters deleted inside a word |
| | `kSecondsPerDay` | Hungarian notation |
| | `customerID` | Wrong camel case of "Id" |
| | `n` | Meaningless, outside a ten-line scope |

- Treat an abbreviation or acronym as one word: `loadHttpUrl`, not `loadHTTPURL`, unless a
  platform name demands it (`XMLHttpRequest`).

### Casing by identifier type

| Style | Used for |
|---|---|
| UpperCamelCase | class, interface, type alias, enum, decorator, type parameter, component function |
| lowerCamelCase | variable, parameter, function, method, property, module alias |
| CONSTANT_CASE | module-level constant, including enum values |

- A type parameter may be a single upper case letter (`T`) or UpperCamelCase.
- A structured test name in an xUnit-style framework may use `_` separators
  (`testX_whenY_doesZ`). Vitest test names here are strings, so this does not arise.
- An identifier must not start or end with `_`, and `_` on its own is not an identifier, not even
  for an unused parameter. To skip elements of an array or tuple, leave a gap in the destructuring:
  `const [a, , b] = [1, 5, 10]`.
- A module namespace import is lowerCamelCase (`import * as fooBar from './fooBar'`), and only
  jQuery's `$` and three.js's `THREE` are exempt.

### Constants

- CONSTANT_CASE says a value is not to be changed, even when it is technically mutable, such as an
  object that is not deeply frozen. A constant may also be a `static readonly` class property.
- Only a module-level symbol, a static field of a module-level class, or a value of a module-level
  enum may use CONSTANT_CASE. A value that can be created more than once in the program's lifetime,
  such as a local inside a function, is lowerCamelCase.
- An arrow function implementing an interface may be lowerCamelCase.

```ts
const EURO_INPUT = /^\d+(?:[.,]\d{1,2})?$/
```

### Aliases

- A local alias of an existing symbol keeps the existing symbol's format. Use `const` for a local
  alias and `readonly` for a class field alias.

```ts
const { BrewStateEnum } = SomeType
const CAPACITY = 5

class Teapot {
  readonly BrewStateEnum = BrewStateEnum
  readonly CAPACITY = CAPACITY
}
```

## Type system

### Type inference and annotations

- Code may rely on inference for every type expression: variables, fields, return types. Leave out
  an annotation the initializer already makes trivial: a string, number, boolean, `RegExp` literal
  or `new` expression. `const x: boolean = true` and `const x: Set<string> = new Set()` say
  nothing; `const x = new Set<string>()` is needed so the parameter is not inferred as `unknown`.
- An annotation helps where the expression is hard to read at a glance:
  `const value: string[] = await rpc.getSomeValue().transform()`. The reviewer decides.
- Whether to annotate a return type is the author's choice; a reviewer may ask for one on a complex
  return type. An explicit return type documents the function and surfaces a type change sooner.

### Undefined and null

- Both `undefined` and `null` exist as types, written as unions (`string | null`). There is no
  general preference between them; many JavaScript APIs use `undefined` (`Map.get`) and many DOM
  APIs use `null` (`Element.getAttribute`), so follow the context.
- A type alias must not include `| null` or `| undefined`. A nullable alias means nulls are being
  passed through too many layers, and it hides which specific value may be absent. Add the union
  where the alias is used, and deal with the null close to where it arises.

```ts
type CoffeeResponse = Latte | Americano

class CoffeeService {
  getLatte(): CoffeeResponse | undefined { ... }
}
```

- Prefer an optional field or parameter (`milk?: Milk`, `function pour(volume?: Milliliter)`) over
  a `| undefined` union. An optional member may be left out when constructing the value or calling.
  In a class, prefer to avoid the pattern and initialize as many fields as possible.

### Structural types

- TypeScript's types are structural: a value matches a type when it has at least the required
  properties with matching types. Write the type at the declaration of a value that is meant to
  match a type (`const foo: Foo = {...}`), so an error appears at the declaration rather than at a
  far-away call site.
- Define a structural type with an `interface`, not a class with readonly fields.

### Interfaces over type aliases

- A `type` alias may name a primitive, a union, a tuple or any other type. For an object shape, use
  an `interface`, never a `type` alias of an object literal. The two forms are nearly equivalent, so
  one is chosen, and the TypeScript team itself prefers interfaces for anything they can model.

```ts
interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
}
```

### Arrays

- For a simple element type (alphanumeric characters and dots only), write `T[]` or
  `readonly T[]`, including for multiple dimensions (`string[][]`). For anything more complex,
  write `Array<T>` or `ReadonlyArray<T>`. The rule applies at every level of nesting.

| Write | Not |
|---|---|
| `string[]` | `Array<string>` |
| `readonly string[]` | `ReadonlyArray<string>` |
| `ns.MyObj[]` | `Array<ns.MyObj>` |
| `string[][]` | `Array<string[]>` |
| `Array<{ n: number; s: string }>` | `{ n: number; s: string }[]` |
| `Array<string | number>` | `(string | number)[]` |
| `ReadonlyArray<string | number>` | `readonly (string | number)[]` |
| `InjectionToken<string[]>` | `InjectionToken<Array<string>>` |
| `ReadonlyArray<string[]>` | `readonly string[][]` |
| `Array<readonly string[]>` | `(readonly string[])[]` |

### Index signatures

- An object used as a dictionary is typed with an index signature, and the key gets a meaningful
  label: `{ [fileName: string]: number }`, not `{ [key: string]: number }`. The label is
  documentation only.
- Consider `Map` and `Set` instead. Plain objects have surprising behaviour, and a `Map` can be
  keyed by any type.
- `Record<Keys, ValueType>` is for a statically known set of keys, not for an associative array.

### Mapped and conditional types

- Mapped and conditional types, and the standard operators built on them (`Record`, `Partial`,
  `Readonly`, `Pick`), may be used, but they make the reader evaluate a type expression in their
  head, their evaluation model shifts between compiler versions, and tooling such as find-references
  does not see through `Pick<T, Keys>`.
- Always use the simplest type construct that can express the code. A little repetition is cheaper
  than a complex type expression. Where `Pick<User, 'favoriteIcecream' | 'favoriteChocolate'>`
  would do, spell out `interface FoodPreferences` and let `User extends FoodPreferences`, or nest
  a field.

### any

- `any` is a supertype and subtype of every type and lets every property be dereferenced, so it
  masks severe errors and undoes static typing. Consider not using it. Instead: provide a more
  specific type (an interface for server JSON, a type alias for a repetitive union, an inline
  object type for a complex return, a generic where a library would say "any type"), or use
  `unknown`.
- `unknown` holds any value but cannot be used without narrowing it, which is what a value of
  unknown shape needs. Narrow it with a type guard.
- The source allows a suppressed `any` with a comment in a test, for a partial mock. Here that
  comment cannot be written, so build the partial mock as a typed value instead (`Partial<T>`
  narrowed by a named function, or a small class implementing the interface).

### {}

- `{}` means any non-nullish value, which is rarely what is meant. Use `unknown` for an opaque
  value, `Record<string, T>` for a dictionary, and `object` to exclude the primitives.

### Tuples

- Instead of a `Pair` interface, return a tuple and destructure it:
  `function splitInHalf(input: string): [string, string]`. Often named properties are clearer;
  an inline object type (`{ host: string; port: number }`) gives them without a full interface,
  and `const { host, port } = splitHostPort(address)` reads like a tuple.

### Wrapper types

- Never use `String`, `Boolean`, `Number` as types; always the lowercase primitive. `Object` is
  looser than both `{}` and `object`; use one of those. Never invoke a wrapper with `new`.

### Return-type-only generics

- Avoid an API whose type parameter appears only in the return type. When calling one, always
  specify the type argument explicitly: `request<EstimatesResponse>('/api/estimates')`.

## Toolchain

- Every file must pass type checking with the standard tool chain (`vue-tsc` in this project).
- Do not use `@ts-ignore`, `@ts-expect-error` or `@ts-nocheck`. A compiler error usually points to
  a larger problem that a direct fix resolves. `@ts-expect-error` in a unit test is tolerated by
  the source but suppresses every error on the line; narrow the value instead.

## Comments

The source has a chapter on JSDoc and implementation comments. This repository does not write
either: a name carries what the comment would have said, and the only comment allowed is a short
non-obvious why that the code cannot express. Two points from the chapter survive as naming rules:

- A description that would restate the name and type adds nothing; the name is enough.
- A call whose argument is unclear (`someFunction(obviousParam, true, 'hello')`) is fixed by
  making the function take an interface and destructuring it, so the call site names each value.
  The source offers a `/* shouldRender= */ true` comment as the alternative; here only the
  refactoring is available.

## Policies

- Consistency: for a question the guide does not settle, do what the other code in the same file
  does, then what the other files in the same directory do. A brand new file follows the guide in
  full.
- Reformatting existing code is a trade-off against churn. Do not let opportunistic style fixes
  muddle the focus of a change; promote them to a separate change. A file undergoing significant
  change is expected to end up in style.
- A deprecated symbol is marked `@deprecated` in the source's world; here a deprecated symbol is
  removed in the same change, per the repository's deletion rule.
- Generated code is exempt, except that generated identifiers referenced from hand-written code
  follow the naming rules.
