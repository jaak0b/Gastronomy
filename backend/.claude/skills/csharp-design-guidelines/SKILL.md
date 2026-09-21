---
name: csharp-design-guidelines
description: Read before writing, changing or reviewing any C# in this repository, so that names, types, members, exceptions, parameters, collections and disposal follow the .NET Framework Design Guidelines and the code reads consistently.
---

# C# design guidelines

A checklist condensed from the Framework Design Guidelines by Krzysztof Cwalina and Brad Abrams, as
published on Microsoft Learn. Each group links to the reference file that carries the full rule set of
its chapters with the source's examples.

## Naming

Full rules: [references/naming.md](references/naming.md)

- **Do** use PascalCasing for types, namespaces and all public members, and camelCasing for parameters.
- **Do** favor readability over brevity: `CanScrollHorizontally`, never `ScrollableX`.
- **Do not** use abbreviations, contractions, Hungarian notation, underscores or hyphens in identifiers.
- **Do not** capitalize each word in a closed-form compound: `Endpoint`, `Callback`, `Id`, `Metadata`.
- **Do** name classes and structs with nouns, interfaces with adjective phrases prefixed with `I`.
- **Do** give methods verb or verb-phrase names, and properties noun, noun-phrase or adjective names.
- **Do** name Boolean properties with an affirmative phrase, prefixed with `Is`, `Can` or `Has` only where it adds value.
- **Do** name collection properties with a plural phrase, never a singular followed by `List` or `Collection`.
- **Do not** have a property that matches the name of a `Get` method; one of them should go.
- **Do** name events with a verb in the present tense before the change and the past tense after it: `Closing`, `Closed`.
- **Do** use singular names for simple enums, plural names for flag enums, and no `Enum` or `Flags` suffix.
- **Do** add the suffix `Exception`, `Attribute`, `EventArgs`, `EventHandler`, `Collection` or `Dictionary` to types that derive from or implement those base types.
- **Do** name generic type parameters descriptively with a `T` prefix, or `T` alone when there is only one.
- **Do not** introduce generic type names such as `Element`, `Node`, `Log` or `Message`; qualify them.
- **Do** use `value` for the implicit parameter of a setter and `sender` and `e` for event handler parameters.

## Structure

Full rules: [references/structure.md](references/structure.md)

- **Do** keep a class to one kind of work and one reason to change; **do not** put route mapping, request validation and the database write in one class.
- **Do** take every collaborator through the constructor, and **do not** reach for a static accessor or a service locator inside a method.
- **Consider** splitting a class whose constructor takes more than about four or five dependencies; many injected dependencies signal too many responsibilities.
- **Do** put rules and decisions in `GastronomyApp.Core`, EF Core and external access in `GastronomyApp.Infrastructure`, endpoints and hosting in `GastronomyApp.Api`; `Core` references no other project.
- **Do not** accept an ASP.NET Core, EF Core or SQLite `using` in a `Core` file, nor an `Infrastructure` type outside the composition root in `Api`.
- **Do** let an endpoint only map a route, bind and validate the request, call one service and build the response; business rules and persistence live in the service.
- **Do** keep one type per file with the file named after the type, and **do not** create static helper buckets (house rules).

## Type design

Full rules: [references/type-design.md](references/type-design.md)

- **Do** ensure that each type is a well-defined set of related members, not a random collection of functionality.
- **Avoid** a struct unless it represents a single value, is under 16 bytes, is immutable, and is rarely boxed.
- **Do not** define mutable value types, and **do** implement `IEquatable<T>` on value types.
- **Do** give abstract classes a protected or internal constructor, never a public one.
- **Do** use static classes sparingly and **do not** treat them as a miscellaneous bucket.
- **Avoid** marker interfaces; use an attribute instead.
- **Do** provide at least one implementation and one consumer of every interface you define.
- **Do not** add members to an interface that has shipped; define a new interface.
- **Do** use an enum for a closed set of values, and **do not** use one for an open set.
- **Do** provide a zero value on simple enums, named `None` where that fits.
- **Do not** include sentinel or reserved values in enums.
- **Do** apply `FlagsAttribute` to flag enums, use powers of two, and name the zero value `None`.
- **Avoid** public nested types, and **do not** nest a type that client code instantiates or references outside the container.

## Member design

Full rules: [references/member-design.md](references/member-design.md)

- **Do** give a parameter the same name and position in every overload that shares it.
- **Do** use overloads rather than default arguments, and let short overloads call through to the longest.
- **Do not** overload on `ref` or `out`, and **do not** give overloads similar types with different semantics.
- **Do** make a property get-only when the caller must not change it, and **do not** provide set-only properties.
- **Do** allow properties to be set in any order and postpone validation until they are used together.
- **Do** preserve the previous value if a setter throws, and **avoid** throwing from a getter.
- **Do** make constructors do minimal work: capture the parameters and defer everything else.
- **Do** name a constructor parameter exactly like the property it sets, differing only in casing.
- **Avoid** calling virtual members from a constructor.
- **Do not** throw from a static constructor; **consider** inline static field initializers instead.
- **Do** use `EventHandler<TEventArgs>` for events, raise them from a protected virtual `On...` method, and pass `EventArgs.Empty` rather than null.
- **Do not** provide public or protected instance fields; expose properties.
- **Do not** assign a mutable instance (an array, a list, a stream) to a readonly field and expect it to be constant.
- **Avoid** extension methods on types you own; write instance methods instead.
- **Avoid** an `Extensions` namespace; name the namespace after the feature.
- **Avoid** operator overloads except on types that should feel like primitives, and **do** overload symmetrically.
- **Do not** provide implicit conversions that can lose data or throw.

## Parameters

Full rules: [references/member-design.md](references/member-design.md), section Parameter design

- **Do** take the least derived type that gives the member what it needs: `IEnumerable<T>` before `IList<T>`.
- **Do** use an enum instead of two or more Boolean parameters, and **do not** use a Boolean where a third value might ever appear.
- **Do** validate arguments on every public, protected or explicitly implemented member and throw `ArgumentException` or a subclass, with `ParamName` set.
- **Do** throw `ArgumentNullException` for a null the member does not accept.
- **Do** validate enum arguments by range, and **do not** use `Enum.IsDefined` for it.
- **Avoid** `out` and `ref` parameters, and **do not** pass reference types by reference.
- **Do** place `out` parameters after all by-value and `ref` parameters.
- **Do not** use reserved parameters; add an overload when more input is needed.
- **Do** be aware that null can be passed as a `params` array, and **do not** use `params` when the member modifies the array.

## Exceptions

Full rules: [references/exceptions.md](references/exceptions.md)

- **Do** report execution failures by throwing exceptions, and **do not** return error codes.
- **Do not** use exceptions for normal flow of control; offer a tester (`IsReadOnly`) or a `Try...` member with a Boolean return instead.
- **Do** provide a throwing member beside every `Try...` member.
- **Do not** throw `System.Exception`, `SystemException` or `ApplicationException`.
- **Avoid** catching `System.Exception` except in a top-level handler, and only to rethrow elsewhere.
- **Do** throw `InvalidOperationException` when the object is in the wrong state for the call.
- **Do** throw the most derived `ArgumentException` subtype that applies.
- **Do not** let `NullReferenceException`, `IndexOutOfRangeException` or `AccessViolationException` escape a public member; check arguments first.
- **Do not** have a public member that throws or not depending on an option, and **do not** return exceptions as values.
- **Consider** exception builder methods when the same exception is thrown from several places.
- **Avoid** throwing explicitly from a `finally` block, and **do not** throw from an exception filter.

## Collections and arrays

Full rules: [references/usage-and-patterns.md](references/usage-and-patterns.md), sections Arrays and Collections

- **Do** prefer collections over arrays in public members.
- **Do not** use `List<T>` or `Dictionary<TKey,TValue>` as a public parameter or return type; use `IEnumerable<T>`, `Collection<T>`, `ReadOnlyCollection<T>` or `IDictionary<TKey,TValue>`.
- **Do not** return null from a member that returns a collection; return an empty one.
- **Do not** provide settable collection properties.
- **Do not** return a snapshot from a property; a getter returns a live collection or the member becomes a method.
- **Do not** use a read-only array field or an array property that copies on every read.
- **Avoid** taking `ICollection<T>` only to read `Count`.
- **Do** use the `Collection` or `Dictionary` suffix on custom collection types, without an implementation word such as `LinkedList` in the name.

## Ordering

Repository rule; no source page.

- **Do** order a result by a value the data carries: a sort order, a name, a number.
- **Do not** order by a generated identifier. A `Guid` key carries no order, so `OrderBy(row => row.Id)` hands rows out in a random-looking sequence that means nothing; an id is only ever the last tiebreaker after the meaningful keys.

## Equality and operators

Full rules: [references/usage-and-patterns.md](references/usage-and-patterns.md), section Equality operators

- **Do not** overload one equality operator without the other, and **do** keep `Equals` and `==` semantically identical.
- **Avoid** throwing from an equality operator; return false for null.
- **Avoid** overloading equality on mutable reference types.

## Extensibility and sealing

Full rules: [references/usage-and-patterns.md](references/usage-and-patterns.md), section Designing for extensibility

- **Do not** make a member virtual without a reason you can name, and **do** prefer protected virtual over public virtual.
- **Do not** seal a class without a good reason, and **do not** declare protected or virtual members on a sealed type.
- **Do** treat protected members as public for security and compatibility.
- **Do** prefer `Func<...>` and `Action<...>` over custom delegates for callbacks.
- **Avoid** a `Base` suffix on base classes meant for public use.

## Dispose

Full rules: [references/usage-and-patterns.md](references/usage-and-patterns.md), section Dispose pattern

- **Do** implement `IDisposable` on any type that owns a disposable instance.
- **Do** put all cleanup in `protected virtual void Dispose(bool disposing)` and have `Dispose()` call `Dispose(true)` then `GC.SuppressFinalize(this)`.
- **Do not** make `Dispose()` virtual, and **do not** add other `Dispose` overloads.
- **Do** allow `Dispose(bool)` to run more than once without harm.
- **Avoid** throwing from `Dispose`, and **do** throw `ObjectDisposedException` from members used after disposal.
- **Avoid** finalizers; wrap unmanaged resources in a `SafeHandle` instead.

## Library-only chapters

Assembly naming, serialization attributes, System.Xml usage and WPF dependency properties are
summarized in one paragraph each at the end of [references/usage-and-patterns.md](references/usage-and-patterns.md)
and do not apply to this application.

## Data flow

Inside the backend, data travels as EF entities. Wire records live in `GastronomyApp.Contracts`:
requests are consumed by `Core`, responses are produced only by `Api`, mapped from entities with
Mapster. A record outside `Contracts` exists only when the alternative is a tuple or more than about
five parameters, and only after trying the entity or its id first. Logic that is one expression over
an entity is a method on the entity, not a separate function. No stateless one-method service classes.
Names shared across call sites are constants in one static class. A rule that refuses returns
`ErrorOr<T>`, never a nullable value standing for a refusal; the refusal is an `Error` from a factory
in `Core/Refusals`, and its numeric type is its HTTP status, declared once in `RefusalType`. A handler
is one expression ending in `Match`.

## How to use

Run this checklist over every new or changed type and member before handing the code back, and again
when reviewing someone else's change. Read each name and signature cold, without the body, and test it
against the items above. A name or signature that fails an item is a review finding: report it with
the item it fails, and fix it before the change is reported as done. When an item's short form leaves
room for doubt, open the linked reference and read the full rule with its example.
