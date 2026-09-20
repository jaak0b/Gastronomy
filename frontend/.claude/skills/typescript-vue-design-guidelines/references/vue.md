# Vue guidelines

Condensed from the official Vue style guide, with its four priorities kept as sections and every
rule kept under its own name. The guide's Options API examples are rewritten for
`<script setup lang="ts">`, the only component form this repository uses. The guide itself says it
avoids opinions on JavaScript or HTML in general (semicolons, quote style), so everything outside
the rules below stays with [typescript.md](typescript.md).

## Where the Vue guide overrides the Google guide

Inside a `.vue` file, and in the `.ts` file that imports one, these Vue rules win:

- **Default import of a component.** A single-file component is imported as a default import
  (`import DoorGate from './DoorGate.vue'`), which the Google guide reserves for external code. The
  Vue guide's rule Component name casing in JS shows exactly that form, so it is the rule for
  `.vue` imports in `router/` and in components.
- **PascalCase file names for components.** The Vue guide's rule Single-file component filename
  casing wins over the Google guide's snake_case file names for every `.vue` file.
- **Double quotes in templates.** The Vue guide's rule Quoted attribute values asks for whichever
  quote the script does not use; the script uses single quotes, so templates use double quotes
  around attribute values and single quotes inside the bound expression.

Everything else about the TypeScript inside `<script setup>` follows the Google guide.

## Priority A: Essential

These rules prevent errors. Exceptions should be very rare and made only with expert knowledge of
both JavaScript and Vue.

### Use multi-word component names

A user component name is always multi-word, except the root `App` component. Every HTML element is
a single word, so a multi-word name cannot collide with an existing or future element.

```vue-html
<TodoItem />
```

```vue-html
<Item />
```

### Use detailed prop definitions

A committed prop definition is as detailed as possible and states at least its type. Detailed props
document the component's API, and Vue warns in development when a prop arrives in the wrong shape.
In `<script setup lang="ts">` the type-based form carries the type and whether the prop is required:

```ts
const props = defineProps<{
  totalCents: number
  language: AppLanguage
}>()
```

```ts
const props = defineProps(['status'])
```

Where the guide's runtime `validator` would be used, the closed set of values becomes a union type on
the prop, which the compiler checks at every call site.

### Use keyed `v-for`

`key` with `v-for` is always required on components, to keep internal component state down the
subtree. On elements it keeps behaviour predictable, such as object constancy in animations and
focus on a re-ordered `<input>`. Always add a unique key so the edge cases never need thinking
about; a conscious exception is for a performance-critical case where constancy is not needed.

```vue-html
<li
  v-for="todo in todos"
  :key="todo.id"
>
  {{ todo.text }}
</li>
```

### Avoid `v-if` with `v-for`

Never put `v-if` on the same element as `v-for`. `v-if` has the higher priority, so the iteration
variable does not exist when it is evaluated, and the template throws.

- To filter a list, iterate a computed value that returns the filtered list
  (`activeUsers` instead of `v-if="user.isActive"`).
- To hide a whole list, move the `v-if` to the container element (`ul`, `ol`), or wrap the item in
  `<template v-for="user in users" :key="user.id">` and put the `v-if` on the inner element.

```ts
const activeUsers = computed(() => users.value.filter((user) => user.isActive))
```

```vue-html
<li
  v-for="user in activeUsers"
  :key="user.id"
>
  {{ user.name }}
</li>
```

### Use component-scoped styling

Styles in the top-level `App` component and in layout components may be global; every other
component's styles are scoped. The `scoped` attribute is one way; CSS modules or a class naming
convention such as BEM are others. A component library prefers a class-based strategy over
`scoped`, so its internal styles can be overridden through readable, low-specificity class names.

Beyond scoping, an app-specific or component-specific class prefix (`ButtonClose-icon`) keeps
third-party CSS that targets `button`, `btn` or `icon` away from your elements.

```vue
<template>
  <button class="button button-close">×</button>
</template>

<style scoped>
.button {
  border: none;
}

.button-close {
  background-color: red;
}
</style>
```

## Priority B: Strongly Recommended

These rules improve readability and developer experience in most projects. Code still runs when they
are broken, but a violation should be rare and well-justified.

### Component files

Whenever a build system can concatenate files, each component is in its own file, so it is quick to
find when editing or reviewing how it is used.

```
components/
|- TodoList.vue
|- TodoItem.vue
```

### Single-file component filename casing

File names of single-file components are either always PascalCase or always kebab-case, never
`mycomponent.vue` or `myComponent.vue`. PascalCase matches how components are referenced in script
and templates and autocompletes best; kebab-case avoids trouble on case-insensitive file systems.
This repository uses PascalCase.

### Base component names

Base components (presentational, dumb or pure components) that apply app-specific styling and
conventions all begin with one specific prefix, such as `Base`, `App` or `V`. They contain only HTML
elements, other base components and third-party UI components, and never global state such as a
Pinia store. Their names usually include the element they wrap (`BaseButton`, `BaseTable`), or a
purpose where no element exists (`BaseIcon`). The prefix groups them alphabetically in the editor,
removes the need to invent a second word for a simple wrapper, and allows global registration with
`import.meta.glob('./src/**/Base*.vue', { eager: true })`.

```
components/
|- BaseButton.vue
|- BaseTable.vue
|- BaseIcon.vue
```

```
components/
|- MyButton.vue
|- VueTable.vue
|- Icon.vue
```

### Tightly coupled component names

A child component that only makes sense inside one parent carries the parent's name as a prefix.
Editors sort files alphabetically, so the related files sit together. Nesting the child in a
directory named after the parent is not recommended: it produces many files with similar names and
many sub-directories to browse.

```
components/
|- TodoList.vue
|- TodoListItem.vue
|- TodoListItemButton.vue
```

```
components/
|- TodoList.vue
|- TodoItem.vue
|- TodoButton.vue
```

### Order of words in component names

A component name starts with the highest-level, most general words and ends with the descriptive
modifiers. What is highest-level is contextual to the app. With `SearchButtonClear`,
`SearchButtonRun`, `SearchInputQuery` next to each other, the relationships between components are
visible at a glance in a sorted listing; with `ClearSearchButton` and `RunSearchButton` they are
scattered. Nesting by feature directory is only worth considering in very large apps (a hundred or
more components), because navigating sub-directories takes longer, name conflicts such as two
`ButtonDelete.vue` files make navigation harder, and moving a component breaks relative references.

```
components/
|- SearchButtonClear.vue
|- SearchButtonRun.vue
|- SearchInputQuery.vue
|- SearchInputExcludeGlob.vue
|- SettingsCheckboxTerms.vue
|- SettingsCheckboxLaunchOnStartup.vue
```

### Self-closing components

A component with no content is self-closing in single-file components, string templates and JSX,
and never in in-DOM templates. Self-closing says the component is meant to have no content, not
merely left empty. HTML does not allow custom elements to self-close, so in-DOM templates write
`<my-component></my-component>`.

```vue-html
<MyComponent/>
```

```vue-html
<MyComponent></MyComponent>
```

### Component name casing in templates

In single-file components and string templates, component names are PascalCase; in in-DOM
templates they are kebab-case. PascalCase autocompletes in editors, is two characters distinct
from a single-word HTML element rather than one, and keeps Vue components visibly separate from
web components. A project already invested in kebab-case may use it everywhere.

```vue-html
<MyComponent/>
```

```vue-html
<mycomponent/>
<myComponent/>
```

### Component name casing in JS/JSX

A component name in JS or JSX is PascalCase (`import MyComponent from './MyComponent.vue'`).
It may be kebab-case inside a string only in a simple app that registers everything globally
through `app.component`, since such apps live in in-DOM templates where kebab-case is mandatory.

### Full-word component names

A component name prefers full words over abbreviations. Autocompletion makes a long name cheap and
its clarity is worth far more; an uncommon abbreviation is always avoided.

```
components/
|- StudentDashboardSettings.vue
|- UserProfileOptions.vue
```

```
components/
|- SdSettings.vue
|- UProfOpts.vue
```

### Prop name casing

A prop is declared in camelCase. Inside an in-DOM template it is used in kebab-case. A single-file
component template or JSX may use either camelCase or kebab-case, but only one of them across the
whole application.

```ts
const props = defineProps<{ greetingText: string }>()
```

```vue-html
<WelcomeMessage greeting-text="hi"/>
```

### Multi-attribute elements

An element or component with several attributes spans several lines, one attribute per line, for
the same reason a JavaScript object with several properties does.

```vue-html
<img
  src="https://vuejs.org/images/logo.png"
  alt="Vue Logo"
>
<MyComponent
  foo="a"
  bar="b"
  baz="c"
/>
```

### Simple expressions in templates

A template holds only simple expressions; anything more complex moves into a computed value or a
function. A template describes what should appear, not how the value is computed, and the computed
value can be reused.

```vue-html
{{ normalizedFullName }}
```

```ts
const normalizedFullName = computed(() =>
  fullName.value
    .split(' ')
    .map((word) => word[0].toUpperCase() + word.slice(1))
    .join(' '),
)
```

### Simple computed properties

A complex computed value is split into as many simpler ones as possible. Each small computed value
is easier to test, forces a descriptive name even when not reused, and assumes less about how the
view will use it, so a later requirement (show the discount on its own) needs no refactoring.

```ts
const basePrice = computed(() => manufactureCost.value / (1 - profitMargin.value))
const discount = computed(() => basePrice.value * (discountPercent.value || 0))
const finalPrice = computed(() => basePrice.value - discount.value)
```

```ts
const price = computed(() => {
  const basePrice = manufactureCost.value / (1 - profitMargin.value)
  return basePrice - basePrice * (discountPercent.value || 0)
})
```

### Quoted attribute values

A non-empty HTML attribute value is always inside quotes, single or double, whichever the script does
not use. Unquoted values are legal without spaces, but that leads to avoiding spaces and to less
readable values.

```vue-html
<input type="text">
<AppSidebar :style="{ width: sidebarWidth + 'px' }">
```

```vue-html
<input type=text>
<AppSidebar :style={width:sidebarWidth+'px'}>
```

### Directive shorthands

The shorthands `:` for `v-bind:`, `@` for `v-on:` and `#` for `v-slot` are used always or never,
not mixed within a project. This repository uses them always.

```vue-html
<input
  :value="newTodoText"
  :placeholder="newTodoInstructions"
  @input="onInput"
>
<template #header>...</template>
```

## Priority C: Recommended

Where several equally good options exist, one is chosen for consistency. A different choice is fine
with a good reason, applied consistently.

### Component/instance options order

Component options are ordered consistently. The guide's order for the Options API is: global
awareness (`name`), template compiler options (`compilerOptions`), template dependencies
(`components`, `directives`), composition (`extends`, `mixins`, `provide`/`inject`), interface
(`inheritAttrs`, `props`, `emits`, `expose`), `setup`, local state (`data`, `computed`), events
(`watch`, then the lifecycle hooks in the order they run), non-reactive properties (`methods`), and
rendering (`template`/`render`).

Read into `<script setup>`, where the same things are top-level statements, the order becomes:
imports; `defineOptions` where used; `defineProps`; `defineEmits`; `defineExpose` after the value
it exposes exists; injected values and composables such as `useI18n` and a Pinia store; local
state (`ref`, `reactive`) and computed values; watchers and lifecycle hooks; functions.

### Element attribute order

Attributes of elements and components are ordered consistently. The recommended order:

1. Definition: `is`
2. List rendering: `v-for`
3. Conditionals: `v-if`, `v-else-if`, `v-else`, `v-show`, `v-cloak`
4. Render modifiers: `v-pre`, `v-once`
5. Global awareness: `id`
6. Unique attributes: `ref`, `key`
7. Two-way binding: `v-model`
8. Other attributes: every unspecified bound and unbound attribute
9. Events: `v-on`
10. Content: `v-html`, `v-text`

### Empty lines in component/instance options

One empty line between multi-line properties may help once the script no longer fits on one screen;
none is also fine as long as the component stays easy to read and navigate. In `<script setup>`
this means an empty line between a multi-line `defineProps` and the first `computed`, and between
two multi-line computed values.

### Single-file component top-level element order

A single-file component orders `<script>`, `<template>` and `<style>` consistently, with `<style>`
last, because at least one of the other two is always present. Either `<script>` first or
`<template>` first is fine when the whole project agrees; this repository puts `<script setup>`
first.

```vue
<script setup lang="ts">...</script>
<template>...</template>
<style scoped>...</style>
```

## Priority D: Use with Caution

Some Vue features exist for rare edge cases or for migrating a legacy code base. Overused, they make
code harder to maintain or become a source of bugs.

### Element selectors with `scoped`

Avoid element selectors under `scoped`; prefer class selectors. Vue scopes a style by adding a
unique attribute to each element and rewriting the selector to `button[data-v-f3f3eg9]`, and large
numbers of element-attribute selectors are considerably slower than class-attribute selectors.

```vue
<template>
  <button class="btn btn-close">×</button>
</template>

<style scoped>
.btn-close {
  background-color: red;
}
</style>
```

```vue
<style scoped>
button {
  background-color: red;
}
</style>
```

### Implicit parent-child communication

Props and events are the way a parent and a child communicate, not `$parent` and not a mutated
prop. An ideal Vue application is props down, events up, which keeps the flow of state
understandable. Edge cases exist where two already deeply coupled components get simpler by
mutating a prop, but many simple cases merely look convenient: do not trade the ability to
understand the flow of state for writing less code.

```vue
<script setup lang="ts">
const props = defineProps<{ todo: Todo }>()
const emit = defineEmits<{ 'update:todo': [todo: Todo] }>()

function renameTodo(): void {
  emit('update:todo', { ...props.todo, text: 'renamed by parent' })
}
</script>
```

```vue
<script setup lang="ts">
const props = defineProps<{ todo: Todo }>()

function renameTodo(): void {
  props.todo.text = 'renamed by child'
}
</script>

<template>
  <input v-model="todo.text" />
</template>
```
