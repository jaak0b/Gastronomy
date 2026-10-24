# Member design guidelines

Condensed from the Member Design chapter of the Framework Design Guidelines: overloading, properties,
constructors, events, fields, extension methods, operator overloads and parameters.

## Member overloading

Overloading creates two or more members on the same type that differ only in the number or type of
parameters but share a name. Only methods, constructors, and indexed properties can be overloaded.

```csharp
public static class Console {
    public void WriteLine();
    public void WriteLine(string value);
    public void WriteLine(bool value);
    ...
}
```

- **Do** try to use descriptive parameter names to indicate the default used by shorter overloads.
- **Avoid** arbitrarily varying parameter names in overloads. If a parameter in one overload
  represents the same input as a parameter in another overload, the parameters should have the same
  name.
- **Avoid** being inconsistent in the ordering of parameters in overloaded members. Parameters with
  the same name should appear in the same position in all overloads.
- **Do** make only the longest overload virtual (if extensibility is required). Shorter overloads
  should simply call through to a longer overload.
- **Do not** use `ref` or `out` modifiers to overload members. Some languages cannot resolve such
  calls, and the overloads usually have different semantics and should be two separate methods.
- **Do not** have overloads with parameters at the same position and similar types yet with different
  semantics.
- **Do** allow `null` to be passed for optional arguments.
- **Do** use member overloading rather than defining members with default arguments. Default arguments
  are not CLS compliant.

## Property design

Properties are smart fields: the calling syntax of fields with the flexibility of methods.

- **Do** create get-only properties if the caller should not be able to change the value of the
  property. If the property type is a mutable reference type, the value can still be changed.
- **Do not** provide set-only properties or properties with the setter having broader accessibility
  than the getter. If a getter cannot be provided, implement a method instead, named `Set` followed
  by what the property would have been called: `AppDomain.SetCachePath` instead of a set-only
  `CachePath`.
- **Do** provide sensible default values for all properties, ensuring that the defaults do not result
  in a security hole or terribly inefficient code.
- **Do** allow properties to be set in any order even if this results in a temporary invalid state of
  the object. Postpone exceptions for an invalid combination until the properties are actually used
  together.
- **Do** preserve the previous value if a property setter throws an exception.
- **Avoid** throwing exceptions from property getters. Getters should be simple operations without
  preconditions; a getter that can throw should probably be a method. Indexers are the exception,
  because they validate their arguments.

### Indexed property design

Indexers should be used only in APIs that provide access to items in a logical collection, as the
indexer on `System.String` gives access to its characters.

- **Consider** using indexers to provide access to data stored in an internal array.
- **Consider** providing indexers on types representing collections of items.
- **Avoid** using indexed properties with more than one parameter. If the design needs them, reconsider
  whether the property is really an accessor to a logical collection; if not, use methods named with
  `Get` or `Set`.
- **Avoid** indexers with parameter types other than `System.Int32`, `System.Int64`, `System.String`,
  `System.Object`, or an enum. Otherwise, reevaluate and use a method.
- **Do** use the name `Item` for indexed properties unless there is an obviously better name (such as
  the `Chars[]` property on `System.String`). In C#, indexers are named `Item` by default and
  `IndexerNameAttribute` can change it.
- **Do not** provide both an indexer and methods that are semantically equivalent.
- **Do not** provide more than one family of overloaded indexers in one type. The C# compiler enforces
  this.
- **Do not** use nondefault indexed properties. The C# compiler enforces this.

### Property change notification events

- **Consider** raising change notification events when property values in high-level APIs (usually
  designer components) are modified. Low-level APIs such as base types or collections rarely earn the
  overhead: `List<T>` does not raise an event when `Count` changes.
- **Consider** raising change notification events when the value of a property changes via external
  forces, in a way other than by calling methods on the object, as the `Text` of a text box changes
  when the user types.

## Constructor design

Type constructors are static and run before the type is used; instance constructors run when an
instance is created. Constructors are the most natural way to create instances; developers try them
before factory methods.

- **Consider** providing simple, ideally default, constructors. A simple constructor has a very small
  number of parameters, and all parameters are primitives or enums.
- **Consider** using a static factory method instead of a constructor if the semantics of the desired
  operation do not map directly to the construction of a new instance, or if following the constructor
  design guidelines feels unnatural.
- **Do** use constructor parameters as shortcuts for setting main properties. The empty constructor
  followed by property sets must mean the same as the constructor with arguments.
- **Do** use the same name for constructor parameters and a property if the constructor parameters
  are used to simply set the property. The only difference should be casing.
- **Do** minimal work in the constructor. Capture the parameters and delay any other processing until
  required.
- **Do** throw exceptions from instance constructors, if appropriate.
- **Do** explicitly declare the public parameterless constructor in classes, if such a constructor is
  required. Adding a parameterized constructor removes the compiler-generated one, which often causes
  accidental breaking changes.
- **Avoid** explicitly defining parameterless constructors on structs. Array creation is faster
  without one, and many compilers, including C#, do not allow it.
- **Avoid** calling virtual members on an object inside its constructor. The most derived override
  runs before the most derived constructor has finished.

### Type constructor guidelines

- **Do** make static constructors private. The CLR calls the static constructor before the first
  instance is created or a static member is used; a non-private one could be called by other code.
  The C# compiler forces static constructors to be private.
- **Do not** throw exceptions from static constructors. The type becomes unusable in the current
  application domain.
- **Consider** initializing static fields inline rather than explicitly using static constructors,
  because the runtime is able to optimize the performance of types that do not have an explicitly
  defined static constructor.

## Event design

Events are the most commonly used form of callbacks. Pre-events are raised before a state change
(`Form.Closing`) and post-events after it (`Form.Closed`).

- **Do** use the term "raise" for events rather than "fire" or "trigger".
- **Do** use `System.EventHandler<TEventArgs>` instead of manually creating new delegates to be used
  as event handlers.
- **Consider** using a subclass of `EventArgs` as the event argument, unless you are absolutely sure
  the event will never need to carry any data to the event handling method, in which case you can use
  the `EventArgs` type directly. An empty subclass can grow properties later without breaking
  compatibility.
- **Do** use a protected virtual method to raise each event. This applies only to nonstatic events on
  unsealed classes. By convention the method is named `On` followed by the event name. A derived class
  may choose not to call the base implementation, so put nothing there the base class needs.
- **Do** take one parameter to the protected method that raises an event, named `e` and typed as the
  event argument class.
- **Do not** pass `null` as the sender when raising a nonstatic event.
- **Do** pass `null` as the sender when raising a static event.
- **Do not** pass `null` as the event data parameter when raising an event. Pass `EventArgs.Empty`
  when there is no data.
- **Consider** raising events that the end user can cancel. This applies only to pre-events; use
  `System.ComponentModel.CancelEventArgs` or a subclass as the event argument.

### Custom event handler design

When `EventHandler<T>` cannot be used, a custom event handler delegate follows these rules.

- **Do** use a return type of `void` for event handlers. A handler may invoke multiple methods on
  multiple objects, so a single return value makes no sense.
- **Do** use `object` as the type of the first parameter of the event handler, and call it `sender`.
- **Do** use `System.EventArgs` or its subclass as the type of the second parameter of the event
  handler, and call it `e`.
- **Do not** have more than two parameters on event handlers.

## Field design

Encapsulation says that data stored inside an object should be accessible only to that object, so that
a field's name or type can change without breaking anything outside the type. Constant and static
read-only fields are excluded because they never need to change.

- **Do not** provide instance fields that are public or protected. Provide properties instead.
- **Do** use constant fields for constants that will never change. The compiler burns const values
  into calling code, so they can never change without risking compatibility.
- **Do** use public static readonly fields for predefined object instances.
- **Do not** assign instances of mutable types to readonly fields. Arrays, most collections, and
  streams are mutable; `Int32`, `Uri`, and `String` are immutable. The readonly modifier prevents
  replacing the instance, not modifying it.

## Extension methods

Extension methods are static methods called with instance syntax; the static class that defines them
is the sponsor class, and its namespace must be imported to use them.

- **Avoid** frivolously defining extension methods, especially on types you do not own. If you own the
  type, prefer regular instance methods; liberal use clutters APIs of types not designed for them.
- **Consider** using extension methods in either of these scenarios: to provide helper functionality
  relevant to every implementation of an interface, written in terms of the core interface (the LINQ
  to Objects operators on `IEnumerable<T>`); or when an instance method would introduce a dependency
  that breaks dependency management rules (a static `Uri.ToUri(this string str)` rather than
  `String.ToUri()`).
- **Avoid** defining extension methods on `System.Object`. Visual Basic users cannot call them with
  extension syntax, because an `Object` reference forces late binding.
- **Do not** put extension methods in the same namespace as the extended type unless it is for adding
  methods to interfaces or for dependency management.
- **Avoid** defining two or more extension methods with the same signature, even if they reside in
  different namespaces.
- **Consider** defining extension methods in the same namespace as the extended type if the type is an
  interface and if the extension methods are meant to be used in most or all cases.
- **Do not** define extension methods implementing a feature in namespaces normally associated with
  other features. Define them in the namespace associated with the feature they belong to.
- **Avoid** generic naming of namespaces dedicated to extension methods (such as "Extensions"). Use a
  descriptive name (such as "Routing") instead.

## Operator overloads

Operator overloads let a type appear as if it were a built-in primitive, and they have been abused for
operations that should be simple methods.

- **Avoid** defining operator overloads, except in types that should feel like primitive (built-in)
  types.
- **Consider** defining operator overloads in a type that should feel like a primitive type, as
  `System.String` defines `==` and `!=`.
- **Do** define operator overloads in structs that represent numbers (such as `System.Decimal`).
- **Do not** be cute when defining operator overloads. Subtracting one `DateTime` from another to get a
  `TimeSpan` is obvious; a union operator over database queries or a shift operator writing to a
  stream is not.
- **Do not** provide operator overloads unless at least one of the operands is of the type defining
  the overload.
- **Do** overload operators in a symmetric fashion. `==` with `!=`, `<` with `>`, and so on.
- **Consider** providing methods with friendly names that correspond to each overloaded operator, for
  languages without operator overloading.

| C# operator | Metadata name | Friendly name |
|---|---|---|
| implicit conversion | op_Implicit | To\<TypeName\> / From\<TypeName\> |
| explicit conversion | op_Explicit | To\<TypeName\> / From\<TypeName\> |
| + (binary) | op_Addition | Add |
| - (binary) | op_Subtraction | Subtract |
| * (binary) | op_Multiply | Multiply |
| / | op_Division | Divide |
| % | op_Modulus | Mod or Remainder |
| ^ | op_ExclusiveOr | Xor |
| & (binary) | op_BitwiseAnd | BitwiseAnd |
| \| | op_BitwiseOr | BitwiseOr |
| && | op_LogicalAnd | And |
| \|\| | op_LogicalOr | Or |
| = | op_Assign | Assign |
| << | op_LeftShift | LeftShift |
| >> | op_RightShift | RightShift |
| == | op_Equality | Equals |
| != | op_Inequality | Equals |
| > | op_GreaterThan | CompareTo |
| < | op_LessThan | CompareTo |
| >= | op_GreaterThanOrEqual | CompareTo |
| <= | op_LessThanOrEqual | CompareTo |
| *= | op_MultiplicationAssignment | Multiply |
| -= | op_SubtractionAssignment | Subtract |
| ^= | op_ExclusiveOrAssignment | Xor |
| <<= | op_LeftShiftAssignment | LeftShift |
| %= | op_ModulusAssignment | Mod |
| += | op_AdditionAssignment | Add |
| &= | op_BitwiseAndAssignment | BitwiseAnd |
| \|= | op_BitwiseOrAssignment | BitwiseOr |
| , | op_Comma | Comma |
| /= | op_DivisionAssignment | Divide |
| -- | op_Decrement | Decrement |
| ++ | op_Increment | Increment |
| - (unary) | op_UnaryNegation | Negate |
| + (unary) | op_UnaryPlus | Plus |
| ~ | op_OnesComplement | OnesComplement |

Overloading `==` is complicated because its semantics must agree with `Object.Equals` and related
members; see the equality operators section in `usage-and-patterns.md`.

### Conversion operators

Conversion operators are unary, static members on either the operand or the return type, and come in
implicit and explicit forms.

- **Do not** provide a conversion operator if such conversion is not clearly expected by the end users.
- **Do not** define conversion operators outside of a type's domain. `Int32`, `Double`, and `Decimal`
  are numeric; `DateTime` is not, so there is no conversion from `Double` to `DateTime`. A constructor
  is preferred in such a case.
- **Do not** provide an implicit conversion operator if the conversion is potentially lossy, such as
  `Double` to `Int32`. An explicit conversion operator may be lossy.
- **Do not** throw exceptions from implicit casts. Users may not be aware a conversion is happening.
- **Do** throw `System.InvalidCastException` if a call to a cast operator results in a lossy conversion
  and the contract of the operator does not allow lossy conversions.

## Parameter design

- **Do** use the least derived parameter type that provides the functionality required by the member.
  A method that enumerates a collection and prints each item takes `IEnumerable`, not `ArrayList` or
  `IList`.
- **Do not** use reserved parameters. A new overload can be added later.
- **Do not** have publicly exposed methods that take pointers, arrays of pointers, or multidimensional
  arrays as parameters.
- **Do** place all `out` parameters following all of the by-value and `ref` parameters (excluding
  parameter arrays), even if it results in an inconsistency in parameter ordering between overloads.
  Out parameters are extra return values, and grouping them together makes the signature easier to
  read.
- **Do** be consistent in naming parameters when overriding members or implementing interface members.

### Choosing between enum and Boolean parameters

- **Do** use enums if a member would otherwise have two or more Boolean parameters.
- **Do not** use Booleans unless you are absolutely sure there will never be a need for more than two
  values.
- **Consider** using Booleans for constructor parameters that are truly two-state values and are simply
  used to initialize Boolean properties.

### Validating arguments

- **Do** validate arguments passed to public, protected, or explicitly implemented members. Throw
  `System.ArgumentException`, or one of its subclasses, if the validation fails. The check may live in
  a lower private or internal routine; the point is that the entire exposed surface checks.
- **Do** throw `ArgumentNullException` if a null argument is passed and the member does not support
  null arguments.
- **Do** validate enum parameters. The CLR allows casting any integer into an enum, so the argument
  may be outside the defined range.
- **Do not** use `Enum.IsDefined` for enum range checks.
- **Do** be aware that mutable arguments might have changed after they were validated. For a
  security-sensitive member, copy, then validate and process the copy.

### Parameter passing

By-value parameters receive a copy of the argument (a copy of the reference for reference types). A
`ref` parameter receives a reference to the actual argument and can modify it. An `out` parameter
starts unassigned and must be assigned before the member returns.

- **Avoid** using `out` or `ref` parameters. They require pointer-like thinking and understanding of
  value versus reference types, and the difference between the two is not widely understood.
- **Do not** pass reference types by reference. A method that swaps references is a rare exception.

### Members with a variable number of parameters

An array parameter expresses a variable number of arguments; the C# `params` keyword lets the caller
pass the elements inline, and it can be added only to the last parameter.

```csharp
public class String {
    public static string Format(string format, params object[] parameters);
}
```

- **Consider** adding the `params` keyword to array parameters if you expect the end users to pass
  arrays with a small number of elements. With many elements, users will not pass them inline anyway.
- **Avoid** using `params` arrays if the caller would almost always have the input already in an
  array. Byte array parameters in the Framework do not use `params` for this reason.
- **Do not** use `params` arrays if the array is modified by the member taking the `params` array
  parameter. The compiler may create a temporary array at the call site, and modifications are lost.
- **Consider** using the `params` keyword in a simple overload, even if a more complex overload could
  not use it.
- **Do** try to order parameters to make it possible to use the `params` keyword.
- **Consider** providing special overloads and code paths for calls with a small number of arguments
  in extremely performance-sensitive APIs, naming the parameters with the singular form of the array
  parameter plus a numeric suffix. Only do this if the entire code path is special-cased.
- **Do** be aware that `null` could be passed as a `params` array argument. Validate before processing.
- **Do not** use the varargs methods, otherwise known as the ellipsis. They are not CLS compliant.

### Pointer parameters

Pointers should not appear in the public surface area of a managed framework except for
interoperability.

- **Do** provide an alternative for any member that takes a pointer argument, because pointers are not
  CLS-compliant.
- **Avoid** doing expensive argument checking of pointer arguments.
- **Do** follow common pointer-related conventions when designing members with pointers. There is no
  need to pass the start index, because pointer arithmetic accomplishes the same result.
