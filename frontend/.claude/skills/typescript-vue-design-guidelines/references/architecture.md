# Architecture guidelines

Condensed from the Vue guide's Composables chapter and Composition API FAQ, the Pinia core concepts
(Defining a Store, State, Getters, Actions) and the Composing Stores cookbook, the Vue Router guide
(Getting Started, Lazy Loading Routes, Navigation Guards), and the Feature-Sliced Design overview
and layer reference (layer definitions and the import rule only). Every rule names its source page
so it can be checked.

The section Folder layout of this repository maps this repository's folders onto the layers of
the last section: `shared/` is the Shared layer, each surface folder is one slice on the Pages and
Features level, and `main.ts` with `App.vue` is the App layer. That mapping is the owner's decision,
not a source rule.

## Where a piece of code belongs

Source: vuejs.org/guide/reusability/composables (What is a Composable, Comparisons with Other
Techniques); vuejs.org/guide/extras/composition-api-faq (More Flexible Code Organization,
Trade-offs); pinia.vuejs.org/core-concepts (Using the store); vuejs.org/guide/reusability/composables
(tip under Mouse Tracker Example)

- **A plain module** holds stateless logic: a function that takes input and immediately returns the
  expected output, such as a date formatter. The Vue guide names lodash and date-fns as libraries of
  this kind. `parseEuroInput(typed)` in `shared/core/money.ts` is that shape: no reactivity, no lifecycle.
- **A composable** holds stateful logic: state that changes over time, tracked with the Composition
  API (a mouse position, a touch gesture, a connection status). Each component instance that calls
  a composable gets its own copy of that state, so composables do not share state between components.
- **A Pinia store** holds state shared between components. The Composables chapter sends "state
  shared between components" to state management, and a store is created once per application when
  `useXStore()` is first called from a `<script setup>`.
- **A component** reuses both logic and visual layout. The guide's rule: use a composable when
  reusing pure logic, a component when reusing logic and layout together. A renderless component
  costs an extra component instance per use, which a composable does not.
- Composition API code is ordinary JavaScript, so ordinary code organization applies to it: the FAQ
  says you can and should apply any code organization practice to Composition API code that you
  would apply to plain JavaScript, and that the flexibility exists so a logical concern can be moved
  into an external file with minimal effort. A component that has grown past navigating and
  reasoning about is split into composables by logical concern, and those act as component-scoped
  services that talk to one another.
- Mixins are not used in Vue 3: the source of an injected property is unclear, keys collide, and
  mixins communicate implicitly through shared keys. The Class API is not recommended either.

## Composables

### Naming

Source: vuejs.org/guide/reusability/composables (Conventions and Best Practices, Naming)

- A composable is a camelCase function whose name starts with `use`: `useMouse`, `useFetch`,
  `useKeyboardInset`.

### Input arguments

Source: vuejs.org/guide/reusability/composables (Input Arguments, Accepting Reactive State)

- A composable may accept a ref or a getter as an argument even when it does not need reactivity.
  One meant for other developers handles a ref, a getter or a plain value with `toValue()`.
- When the composable creates a reactive effect from its input, it either watches the ref or getter
  explicitly with `watch()`, or calls `toValue()` inside a `watchEffect()` so the dependency is
  tracked. `toValue(url)` is called inside the effect callback, never outside it.

### Return values

Source: vuejs.org/guide/reusability/composables (Return Values, vs. Mixins)

- A composable always returns a plain, non-reactive object holding several refs, so a component can
  destructure it and keep reactivity: `const { x, y } = useMouse()`. Returning a `reactive()` object
  breaks that destructure. A consumer that prefers property access wraps the result:
  `const mouse = reactive(useMouse())`.
- The refs-and-destructure pattern is also what makes the source of every property visible in the
  consuming component, which is the first reason mixins were dropped.

### Side effects and lifecycle

Source: vuejs.org/guide/reusability/composables (Side Effects)

- A composable may perform side effects such as adding a DOM event listener or fetching data.
- Every side effect is cleaned up in `onUnmounted()`: a composable that adds a listener removes it
  there. A small composable that does this itself, like the guide's `useEventListener`, is a good
  idea.
- In an application with server-side rendering, DOM-specific side effects go into post-mount hooks
  such as `onMounted()`. This application renders in the browser only, so the rule does not bite
  here; it is kept so nothing is written that would break under SSR.

### Where a composable may be called

Source: vuejs.org/guide/reusability/composables (Usage Restrictions)

- A composable is called only in `<script setup>` or in `setup()`, and synchronously there. In some
  cases it may be called inside a lifecycle hook such as `onMounted()`. Vue needs the active
  component instance in those contexts to register lifecycle hooks and to link watchers so they are
  disposed on unmount.
- `<script setup>` is the only place where a composable may be called after an `await`; the compiler
  restores the instance context there.

### Nesting

Source: vuejs.org/guide/reusability/composables (Mouse Tracker Example, Extracting Composables for
Code Organization)

- A composable may call other composables; complex logic is composed from small isolated units, as
  an application is composed from components. Values returned by one composable are passed to
  another as ordinary arguments.

## Pinia stores

### Defining a store

Source: pinia.vuejs.org/core-concepts (Defining a Store, Using the store)

- A store is defined with `defineStore()` and a unique id, which connects it to the devtools. The
  returned function is named `use` plus the store name plus `Store`: `useEstimatesStore`.
- Each store lives in its own file, so the bundler can split it and TypeScript can infer it.
- A store is not created until `useXStore()` is called, and that call happens inside a component's
  `<script setup>` or `setup()`, like every composable.

### Setup stores and option stores

Source: pinia.vuejs.org/core-concepts (Option Stores, Setup Stores, What syntax should I pick?)

- An option store passes an object with `state`, `getters` and `actions`, read as the `data`,
  `computed` and `methods` of the store. A setup store passes a function in which `ref()` becomes
  state, `computed()` becomes a getter and `function` becomes an action, and returns what it
  exposes.
- A setup store must return every state property. There is no private state: leaving a ref out of
  the return, or returning it as readonly, breaks SSR, devtools and plugins.
- A setup store may create watchers and use any composable, and may `inject()` anything provided at
  the app level, such as the router or the route. It does not return such injected values; a
  component reads them itself with `useRoute()` or `inject()`.
- Either syntax is acceptable; option stores are simpler, setup stores more flexible. This
  repository's stores are setup stores (`defineStore('estimates', () => { ... })`).

### Reading a store

Source: pinia.vuejs.org/core-concepts (Using the store, Destructuring from a Store)

- A store object is `reactive`, so getters need no `.value`, and it cannot be destructured without
  losing reactivity. To take reactive properties out, use `storeToRefs(store)`; actions may be
  destructured directly because they are bound to the store.

### State

Source: pinia.vuejs.org/core-concepts/state

- Every piece of state is declared in the store's initial state, even when its initial value is
  `undefined`; a property not declared there cannot be added later.
- State may be read and written directly through the store (`store.count++`, bound to `v-model`),
  or changed in a group with `$patch`, which takes a partial state object or a function for
  collection edits and records one devtools entry. Both direct writes and `$patch` are tracked.
- The state cannot be replaced; assigning `store.$state` patches it. `$reset()` exists on option
  stores; a setup store defines its own.
- `$subscribe()` watches the state and fires once per patch; it is bound to the component that
  registers it unless `{ detached: true }` is passed.

### Getters

Source: pinia.vuejs.org/core-concepts/getters

- A getter is a computed value over the store's state. In a setup store it is a `computed()`.
- A getter may read other getters and other stores' getters by calling their `useXStore()` inside it.
- A getter cannot take arguments; a getter that returns a function can, but is then no longer
  cached.

### Actions

Source: pinia.vuejs.org/core-concepts/actions

- Actions are the store's methods and the place for business logic. Unlike getters, an action may be
  asynchronous and may `await` an API call or another action. Arguments and return values are free.
- An action may use another store by calling its `useXStore()` inside the action.
- `$onAction()` observes actions before they run, after they resolve and when they throw; it is
  bound to the registering component unless detached with `true`.

The Pinia sources allow direct writes to state from a component and never restrict changes to
actions; the phrase "perfect to define business logic" is the strongest wording they give. A rule
that only actions may change state would be this repository's own and is not taken from a source.

### Composing stores without cycles

Source: pinia.vuejs.org/cookbook/composing-stores

- Stores may use each other, under one rule: two or more stores that use each other must not form an
  infinite loop through getters or actions, and must not both read each other's state directly in
  their setup functions.
- In a setup store, another store is used at the top of the store function. In an option store, its
  `useXStore()` is called inside the getter or action that needs it.
- Since actions can be asynchronous, every `useXStore()` call inside an action appears before the
  first `await`; afterwards it may bind to the wrong Pinia instance under SSR.

## Router

This application keeps its hand-written router (`shared/router/route.ts`, `shared/router/router.ts`
and `shared/router/backButtonTrap.ts`) and does not adopt Vue Router. The rules below apply to any client-side router and are checked against the
hand-written one. Where a rule cites no source page, it is this repository's own rule.

### One route value, one navigation function

Source: none; this repository's own rule.

- The current route is read from one exported reactive value in the router module and nowhere
  else. No screen derives the route from `window.location` on its own.
- Navigation goes through one exported function of the router module. Nothing outside that module
  touches `window.history` or `window.location` directly.
- A route's shape is a discriminated union type, so every branch on the route name is exhaustive
  and ends in `assertNever`.

### Screen components are loaded up front

Source: none; this is the owner's decision, not a source rule.

- Every screen component is imported at the top of the file that renders it, never through a
  dynamic import. The whole bundle is small, it is served from the laptop on site over the
  festival's own WiFi, and a chunk fetched only when a screen opens could fail at the moment a
  server taps it. The Vue Router page asks for a dynamic import per route component, and it is not
  followed here for that reason.

### Where a navigation decision lives

Source: router.vuejs.org/guide/advanced/navigation-guards (Global Before Guards, Per-Route Guard)

- Whether a navigation may happen (the device is not enrolled, the admin screens open only on the
  laptop) is decided in the router module, not inside a screen. The Vue Router page places such
  decisions in guards registered on the router or on a route, which run before the screen exists;
  the hand-written router keeps them in the same place for the same reason.
- A decision returns one of three outcomes: let the navigation through, cancel it, or redirect to
  another route.

### The back-button trap is a separate concern

Source: none; this repository's own rule.

- The back-button trap lives in `shared/router/backButtonTrap.ts`, its own module beside the
  router. It never imports the router: what it needs when the anchor comes back is passed to it as
  arguments.

## Layers and the import rule

Source: feature-sliced.design/docs/get-started/overview (Layers);
feature-sliced.design/docs/reference/layers (Import rule on layers, Layer definitions)

Layers are the first level of organisation and separate code by how much responsibility it carries
and how many other modules it depends on. Their names are standardized; adding a layer is not
recommended, and a project uses only the layers that bring it value. Most projects have at least
Shared, Pages and App.

**The import rule.** A module in a slice may import from other slices only when they are on a layer
strictly below its own. A file in `features/aaa` cannot import from `features/bbb`, but can import
from `entities` and `shared`, and from any sibling file inside `features/aaa`. A module never
imports from its own layer's other slices or from a layer above. App and Shared are the exception:
each is a layer and a slice at once, has no business domains, and so its segments import each
other freely.

From top (most responsibility) to bottom (least):

- **App.** Everything that makes the app run: routing, entrypoints, global styles, providers, and
  app-wide business matters such as analytics. It holds no slices, only segments, typically
  `routes` (the router configuration), `store` (global store configuration), `styles` and
  `entrypoint`.
- **Processes.** Deprecated. Escape hatches for multi-page interactions; the current spec moves their
  contents to Features and App and keeps router-level and server-level logic on App.
- **Pages.** Full pages, screens or large parts of a page in nested routing. One page is usually one
  slice; several very similar pages, such as registration and login, may share one. A page slice
  holds the page's UI with its loading states and error boundaries, and its data fetching and
  mutating requests. A UI block that is never reused stays inside the page; a page rarely has its
  own data model, and tiny bits of state stay in the components.
- **Widgets.** Large self-sufficient blocks of UI, most useful when reused across pages or when a
  page has several large independent blocks. A block that makes up most of a page and is never
  reused is not a widget; it lives in the page.
- **Features.** The main interactions users care to do, usually involving business entities. Not
  everything is a feature: reuse on several pages is the indicator, and too many features drown the
  important ones. A newcomer should discover the app by reading its pages and features. A feature
  slice holds the UI of the interaction (a form), its API calls, its validation and internal state,
  and its feature flags.
- **Entities.** Concepts from the real world the project works with, in the business's own terms
  (User, Post, Group). An entity slice holds data storage and validation schemas (`model`),
  entity-related requests (`api`) and the entity's visual representation (`ui`) meant for reuse
  across pages, with business logic attached through props or slots. Slices cannot know each other,
  so interaction between entities is kept in Features or Pages; where one entity's data contains
  another's, the connection is made explicit through a cross-reference (`@x`) API so the two are
  refactored together.
- **Shared.** The foundation: connections to the outside world (backends, third-party libraries, the
  environment) and the project's own contained libraries. It has no slices, so all its files may
  import each other. Typical segments: `api` (the API client and request functions), `ui` (the UI
  kit, business-themed but without business logic), `lib` (internal libraries with one area of
  focus each, never a helpers or utilities dump), `config`, `routes` (route constants), `i18n`. A
  segment is named for its purpose, never for its essence: `components`, `hooks` and `types` are
  bad segment names.

## Folder layout of this repository

Source: none; this is the owner's decision, not a source rule.

`src` is split by surface first, then by type inside each surface:

- `src/phone/`: the server's phone app (order building, review, open items).
- `src/station/`: the station tablet page.
- `src/admin/`: the configuration UI.
- `src/shared/`: everything two or more surfaces use (the api client, i18n, device token handling,
  the request gate, the router, shared components and composables).

Inside each surface folder there are `components/`, `composables/`, `stores/` and `views/` as
needed, and nothing deeper unless a folder passes about ten files.

**Import rule.** A surface imports only from `shared/` and from itself. A surface never imports
from another surface. `shared/` never imports from a surface.

**Mapping onto the layers.** `shared/` is the Shared layer, each surface folder is one slice on the
Pages and Features level, and `main.ts` with `App.vue` is the App layer.
