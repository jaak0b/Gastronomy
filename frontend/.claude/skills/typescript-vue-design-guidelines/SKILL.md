---
name: typescript-vue-design-guidelines
description: Read before writing, changing or reviewing any TypeScript or Vue file in this repository, so that modules, names, types, classes, functions, errors, components, props and templates follow the Google TypeScript Style Guide, the TypeScript declaration Do's and Don'ts and the official Vue style guide, and the code reads consistently.
---

# TypeScript and Vue design guidelines

A checklist condensed from three sources: the Google TypeScript Style Guide, the "Do's and Don'ts"
page of the TypeScript handbook, and the official Vue 3 style guide. Each group below links to the
reference file that carries the full rule set with the source's examples, rewritten for
`<script setup lang="ts">`, Pinia and Vitest where the source shows another form.

Read it before the first edit to a `.ts` or `.vue` file, before a rename, before a new test, and
before a review.

## Which source wins

- A `.ts` file follows the Google guide, with the declaration Do's and Don'ts on top.
- A `.vue` file follows the Vue guide for everything Vue decides (component names, props, emits,
  templates, styles, file order), and the Google guide for the TypeScript inside `<script setup>`.
- Where the two disagree on the same point, the Vue guide wins inside a `.vue` file and the Google
  guide wins inside a `.ts` file. The known disagreements are listed in
  [references/vue.md](references/vue.md), section Where the Vue guide overrides the Google guide.

## What this repository changes

The root `CLAUDE.md` overrides the sources on three points, and the reference files already carry
these changes.

- **No code comments and no JSDoc.** The Google guide's comment and JSDoc chapters are replaced by
  one rule: a name that needs a comment is wrong, so rename until the comment is redundant, then
  delete it. The only comment allowed is a short non-obvious why that the code cannot express. Every
  place where the Google guide says "add a comment explaining why" (an `any`, a type assertion, an
  empty catch, a non-printable escape) reads here as "restructure so the reason is visible, or
  write the one allowed why".
- **No em-dash character** anywhere, in code, in strings, in tests and in this skill.
- **Semicolons and file names** follow the file you are in. The Google guide asks for a semicolon
  after every statement and snake_case file names; this codebase's files have neither, and the
  guide's own consistency policy says to do what the surrounding code already does. New files match
  their folder: camelCase `.ts` names, PascalCase `.vue` names.

## Source files and modules

Full rules: [references/typescript.md](references/typescript.md), sections Source files and Modules

- **Do** order a file as imports, then the implementation, with one blank line between the two.
- **Do** import with a relative path inside the project, and **do** keep the number of `../` steps low.
- **Do** use named imports for symbols used often or with clear names, and a namespace import for many symbols from a large module or for common names such as `Model`.
- **Do** use named exports only; **do not** write `export default`.
- **Do** export only what another file uses, and **do not** export a `let`.
- **Do not** create a class of static members for namespacing; export the constants and functions themselves.
- **Do** use `import type` for a symbol used only as a type and `export type` when re-exporting a type.
- **Do not** use `namespace`, `module`, `/// <reference>` or `import x = require()`.

## Naming

Full rules: [references/typescript.md](references/typescript.md), section Naming

- **Do** use UpperCamelCase for classes, interfaces, type aliases, enums and type parameters, lowerCamelCase for variables, parameters, functions, methods, properties and module aliases, and CONSTANT_CASE for module-level constants and enum values.
- **Do** treat an acronym as one word: `loadHttpUrl`, `customerId`, never `loadHTTPURL` or `customerID`.
- **Do** write descriptive names a new reader understands, and **do not** abbreviate by deleting letters or with a term only this project knows.
- **Do not** prefix an interface with `I` or suffix it with `Interface`; name it for why it exists.
- **Do not** put type information into a name (Hungarian notation, `opt_` on optional parameters).
- **Do not** use a leading or trailing underscore, and **do not** use `_` as an identifier.
- **Do** keep a local alias in the format of the symbol it aliases.

## Type system

Full rules: [references/typescript.md](references/typescript.md), section Type system, and
[references/declarations.md](references/declarations.md)

- **Do** leave out a type annotation the initializer already makes obvious, and **do** add one where the expression is hard to read.
- **Do** declare object shapes with `interface`, never with a `type` alias of an object literal.
- **Do** write the type at the declaration of an object literal (`const foo: Foo = {...}`), not with `as Foo`.
- **Do not** use `any`; use a specific type, or `unknown` and narrow it.
- **Do not** use `{}`; use `unknown`, `Record<string, T>` or `object`.
- **Do not** use `String`, `Number`, `Boolean`, `Symbol` or `Object` as types, and never `new` on a wrapper.
- **Do** use optional (`?`) fields and parameters rather than `|undefined`, and **do not** bake `|null` or `|undefined` into a type alias.
- **Do** write `T[]` and `readonly T[]` for simple element types, `Array<T>` and `ReadonlyArray<T>` for compound ones.
- **Do** give an index signature key a meaningful label, and **consider** `Map` or `Set` instead.
- **Do** use the simplest type construct that expresses the code; a spelled-out interface beats a mapped or conditional type.
- **Do** use a tuple rather than a `Pair` interface, and an inline object type where names help.
- **Do not** declare a generic that does not use its type parameter, and **avoid** return-type-only generics.
- **Do not** write `@ts-ignore`, `@ts-expect-error` or `@ts-nocheck`.

## Classes

Full rules: [references/typescript.md](references/typescript.md), section Classes

- **Do** use `private`, never `#private` fields, and **do not** bypass visibility with `obj['foo']`.
- **Do** mark a property never reassigned outside the constructor `readonly`.
- **Do** use a parameter property instead of a field assigned from a constructor parameter, and initialize other fields where they are declared.
- **Do not** write the `public` modifier except on a non-readonly public parameter property.
- **Do not** write an empty constructor or one that only calls `super`.
- **Do** call a constructor with parentheses: `new Foo()`.
- **Do** keep a getter pure, and **do not** write a pass-through getter and setter pair to hide a property.
- **Do not** use `this` in a static context, and **prefer** a module-local function over a private static method.
- **Do** limit visibility as far as possible; a private method can be a non-exported function in the same file.
- **Do not** manipulate prototypes or modify builtin objects.

## Functions

Full rules: [references/typescript.md](references/typescript.md), section Functions

- **Do** declare a named function with `function`; an arrow function is for callbacks, nested functions in methods, and where a type annotation demands it.
- **Do not** use a `function` expression; use an arrow function.
- **Do** use a block body when the return value is unused, and a concise body only when it is used.
- **Do** pass an arrow function that forwards the parameters rather than a named callback whose signature might not match: `map((n) => parseInt(n))`.
- **Avoid** arrow functions as class properties, except for an event handler that must be uninstalled later.
- **Do** keep a default parameter initializer simple and side-effect free, and **prefer** a destructured options object over several optional parameters.
- **Do** use a rest parameter, never `arguments`, and spread syntax, never `apply`.
- **Do** use `this` only in constructors, methods, functions with an explicit `this` type, and arrow functions inside those.

## Control flow and language features

Full rules: [references/typescript.md](references/typescript.md), section Language features

- **Do** use `const` by default and `let` when reassigned; **never** `var`; one variable per declaration.
- **Do** write `===` and `!==`; the only exception is `== null` to catch both `null` and `undefined`.
- **Do** iterate an array with `for (... of ...)`, and an object with `for ... of Object.keys/values/entries`; **do not** use unfiltered `for ... in`.
- **Do** brace every control flow body; a single-line `if` may omit the braces.
- **Do** give every `switch` a `default` last, and **do not** fall through a non-empty case.
- **Do** use array and object literals, never the `Array` or `Object` constructor, and only spread iterables into arrays and objects into objects.
- **Do** delimit strings with single quotes, use a template literal instead of concatenation, and **do not** use line continuations.
- **Do** coerce with `String()`, `Boolean()`, `!!` or a template literal, parse with `Number()` and check for `NaN`; **do not** use unary `+`, `parseInt` or `parseFloat` for base ten.
- **Do not** coerce an enum value to a boolean; compare it with `!==`.
- **Do not** use `const enum`, `debugger`, `with`, `eval`, `Function(...)`, or a non-standard language feature.
- **Do not** define a decorator; only a framework's own decorators may be used.

## Errors

Full rules: [references/typescript.md](references/typescript.md), section Exception handling

- **Do** throw and reject with `new Error()` or a subclass, never a string or another value.
- **Do** define a subclass of `Error` where the native type carries too little.
- **Do** assume a caught value is an `Error`, catch it as `unknown`, and **do not** defend against other types.
- **Do not** leave a catch block empty; surface, rethrow, or return a value the caller acts on.
- **Do** keep a `try` block to the calls that can throw.

## Declaration shapes

Full rules: [references/declarations.md](references/declarations.md)

- **Do** type a callback whose result is ignored as `() => void`, never `() => any`.
- **Do not** mark a callback's parameter optional unless the callback is really invoked with fewer arguments.
- **Do** write one signature with the highest arity instead of overloads that differ only in callback arity.
- **Do** order overloads from most specific to most general.
- **Do** replace overloads that differ only in trailing parameters with optional parameters, and overloads that differ in one parameter's type with a union.

## Vue components

Full rules: [references/vue.md](references/vue.md)

- **Priority A, essential:** multi-word component names; typed `defineProps`; `:key` on every `v-for`; never `v-if` on the same element as `v-for`; scoped styles outside `App` and layout components.
- **Priority B, strongly recommended:** one component per file; PascalCase `.vue` file names; `Base` prefix for presentational components; parent name prefix for tightly coupled children; general word first in a name; self-closing tags for components without content; PascalCase component tags; full words, no abbreviations; camelCase prop names; one attribute per line on multi-attribute elements; simple template expressions; small computed properties; quoted attribute values; directive shorthands used always.
- **Priority C, recommended:** a consistent order of `defineProps`, `defineEmits`, state, computed values, watchers and functions; a consistent element attribute order; `<style>` last in a single-file component.
- **Priority D, use with caution:** class selectors, not element selectors, under `scoped`; props down and events up, never a mutated prop and never `$parent`.

## Architecture: components, composables, modules, stores, routes, layers

Full rules: [references/architecture.md](references/architecture.md)

- **Do** put stateless logic in a plain module, stateful logic in a composable, state shared between components in a Pinia store, and logic that comes with a visual layout in a component.
- **Do** name a composable `use...`, return a plain object of refs from it, clean up its side effects in `onUnmounted()`, and call it only synchronously in `<script setup>` or `setup()`.
- **Do** define each store in its own file with `defineStore()` and a unique id, named `useXStore`, and return every state property from a setup store.
- **Do** read reactive store properties through `storeToRefs()`, never by destructuring the store.
- **Do** put business logic in actions, and **do not** let two stores read each other's state in their setup functions or loop through getters or actions; every `useXStore()` inside an action comes before the first `await`.
- **Do** read the current route from the router module's one exported reactive value and navigate through its one exported function; **do not** touch `window.history` or `window.location` outside the router module, and **do** type a route as a discriminated union.
- **Do** import every screen component up front, never through a dynamic import: the bundle is small, it is served from the laptop on site over the festival's own WiFi, and a chunk fetched later could fail at the moment a server taps a screen. That is the owner's decision, not a source rule.
- **Do** keep the decision whether a navigation may happen in the router module, not in a screen; the back-button trap is its own module beside the router.
- **Do** import only from layers strictly below your own, never sideways to another slice of the same layer and never upward; App and Shared segments import each other freely.
- **Do** put a file under the surface it serves (`src/phone/`, `src/station/`, `src/admin/`) or under `src/shared/` when two or more surfaces use it; a surface imports only from `shared/` and itself, and `shared/` never imports from a surface.

## Ordering

Repository rule; no source page.

- **Do** render a list in the order the endpoint returned it when the backend already ordered it.
- **Do** sort in the app only for a view the backend never returns as one list, such as an aggregate built from several orders.

## How to use

Run this checklist over every new or changed module, type, function and component before handing
the code back, and again when reviewing someone else's change. Read each name and signature cold,
without the body, and test it against the items above. A name or signature that fails an item is a
review finding: report it with the item it fails, and fix it before the change is reported as done.
When an item's short form leaves room for doubt, open the linked reference and read the full rule
with its example.
