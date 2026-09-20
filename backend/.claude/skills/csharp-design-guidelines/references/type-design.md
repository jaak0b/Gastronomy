# Type design guidelines

Condensed from the Type Design chapter of the Framework Design Guidelines: class versus struct,
abstract classes, static classes, interfaces, structs, enums and nested types.

Classes are the general case of reference types. Interfaces can be implemented by both reference and
value types and stand in for multiple inheritance. Structs are the general case of value types and are
reserved for small, simple types similar to language primitives. Enums define short sets of values.
Static classes are containers for static members.

- **Do** ensure that each type is a well-defined set of related members, not just a random collection
  of unrelated functionality.

## Choosing between class and struct

Value types are allocated on the stack or inline and are cheaper to allocate and free; arrays of them
are allocated inline with better locality. They box when cast to a reference type or an interface, and
heavy boxing hurts the heap and the collector. Assignment copies the whole value, and they are passed by
value, so a mutable value type confuses callers who change a copy without noticing. As a rule of thumb,
the majority of types in a framework should be classes.

- **Consider** defining a struct instead of a class if instances of the type are small and commonly
  short-lived or are commonly embedded in other objects.
- **Avoid** defining a struct unless the type has all of the following characteristics: it logically
  represents a single value, similar to primitive types (int, double); it has an instance size under
  16 bytes; it is immutable; it will not have to be boxed frequently. In all other cases, define a
  class.

## Abstract class design

- **Do not** define public or protected internal constructors in abstract types. Nobody can create an
  instance of an abstract type, so a public constructor is misleading.
- **Do** define a protected or an internal constructor in abstract classes. A protected constructor
  lets the base class initialize itself when subtypes are created; an internal constructor limits
  concrete implementations to the defining assembly.
- **Do** provide at least one concrete type that inherits from each abstract class that you ship. This
  validates the design, as `FileStream` validates `Stream`.

## Static class design

A static class contains only static members. In C#, a class declared `static` is sealed, abstract, and
cannot declare instance members. Static classes are a compromise between pure object-oriented design
and simplicity, used for shortcuts to other operations (`System.IO.File`), holders of extension
methods, or functionality for which a full object-oriented wrapper is unwarranted
(`System.Environment`).

- **Do** use static classes sparingly, only as supporting classes for the object-oriented core.
- **Do not** treat static classes as a miscellaneous bucket.
- **Do not** declare or override instance members in static classes.
- **Do** declare static classes as sealed, abstract, and add a private instance constructor if your
  programming language does not have built-in support for static classes.

## Interface design

The CLR does not support multiple inheritance, but a type may implement several interfaces, so
interfaces achieve the same effect: `IDisposable` lets any type support disposal regardless of its
base class. An interface is also the only option for a common base shared by value types.

- **Do** define an interface if you need some common API to be supported by a set of types that
  includes value types.
- **Consider** defining an interface if you need to support its functionality on types that already
  inherit from some other type.
- **Avoid** using marker interfaces (interfaces with no members). Mark a class with a custom attribute
  instead.
- **Do** provide at least one type that is an implementation of an interface, as `List<T>`
  implements `IList<T>`.
- **Do** provide at least one API that consumes each interface you define (a method taking the
  interface as a parameter or a property typed as the interface), as `List<T>.Sort` consumes
  `IComparer<T>`.
- **Do not** add members to an interface that has previously shipped. Create a new interface instead.

Except for the situations described here, choose classes rather than interfaces.

## Struct design

- **Do not** provide a parameterless constructor for a struct. Arrays of structs can then be created
  without running the constructor on each item.
- **Do not** define mutable value types. A property getter returns a copy, and developers mutate the
  copy without noticing.
- **Do** ensure that a state where all instance data is set to zero, false, or null (as appropriate)
  is valid, so an array of the struct never holds invalid instances.
- **Do** implement `IEquatable<T>` on value types. `Object.Equals` on a value type boxes and its
  default implementation uses reflection.
- **Do not** explicitly extend `ValueType`. Most languages prevent this.

Structs are useful only for small, single, immutable values that will not be boxed frequently.

## Enum design

Simple enums represent small closed sets of choices, such as colors. Flag enums support bitwise
operations on their values, such as a list of options.

- **Do** use an enum to strongly type parameters, properties, and return values that represent sets of
  values.
- **Do** favor using an enum instead of static constants.
- **Do not** use an enum for open sets (such as the operating system version, names of your friends).
- **Do not** provide reserved enum values that are intended for future use. Values can be added later;
  reserved values pollute the set and lead to user errors.
- **Avoid** publicly exposing enums with only one value. Method overloading allows adding parameters
  later; a single-value enum as a reserved parameter belongs to C, not managed code.
- **Do not** include sentinel values in enums. They track the state of the enum rather than being one
  of its values and confuse users.
- **Do** provide a value of zero on simple enums. Call it something like "None", or give the most
  common default the underlying value of zero.
- **Consider** using `Int32` (the default in most programming languages) as the underlying type of an
  enum unless the enum is a flags enum with more than 32 flags, the type must differ for unmanaged
  interop, or a smaller type saves substantial space because the enum is a field in a very frequently
  instantiated type, is stored in large arrays, or is serialized in large numbers. Managed objects are
  DWORD-aligned, so a smaller enum only helps when packed with other small fields.
- **Do** name flag enums with plural nouns or noun phrases and simple enums with singular nouns or
  noun phrases.
- **Do not** extend `System.Enum` directly. Use the `enum` keyword.

### Designing flag enums

- **Do** apply the `System.FlagsAttribute` to flag enums. Do not apply this attribute to simple enums.
- **Do** use powers of two for the flag enum values so they can be freely combined using the bitwise
  OR operation.
- **Consider** providing special enum values for commonly used combinations of flags, such as
  `ReadWrite`, so simple tasks need no bitwise operations.
- **Avoid** creating flag enums where certain combinations of values are invalid.
- **Avoid** using flag enum values of zero unless the value represents "all flags are cleared" and is
  named appropriately.
- **Do** name the zero value of flag enums `None`. For a flag enum, the value must always mean "all
  flags are cleared".

### Adding values to enums

Adding a value after shipping carries a small compatibility risk when an existing API returns it to an
application that does not handle it.

- **Consider** adding values to enums, despite a small compatibility risk. With real data about
  incompatibilities, add a new API that returns the new and old values and deprecate the old one.

## Nested types

A nested type is defined within the scope of an enclosing type and has access to all its members,
including private ones. Nested types are tightly coupled to their enclosing type and are best suited
for modeling implementation details of it, such as a collection's enumerator, which the end user rarely
declares and almost never instantiates.

- **Do** use nested types when the relationship between the nested type and its outer type is such
  that member-accessibility semantics are desirable.
- **Do not** use public nested types as a logical grouping construct; use namespaces for this.
- **Avoid** publicly exposed nested types. The only exception is if variables of the nested type need
  to be declared only in rare scenarios such as subclassing or other advanced customization.
- **Do not** use nested types if the type is likely to be referenced outside of the containing type.
  An enum passed to a method on a class should not be nested in the class.
- **Do not** use nested types if they need to be instantiated by client code. If a type has a public
  constructor, it should probably not be nested.
- **Do not** define a nested type as a member of an interface. Many languages do not support such a
  construct.
