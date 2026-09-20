# Usage guidelines, extensibility and the dispose pattern

Condensed from three chapters of the Framework Design Guidelines: Designing for Extensibility, the
Usage Guidelines (arrays, attributes, collections, equality operators) and the Dispose Pattern from
Common Design Patterns. Serialization, System.Xml usage and dependency properties are summarized in
one paragraph each at the end.

## Designing for extensibility

Extensibility mechanisms range from cheap and weak to expensive and powerful. Choose the least costly
mechanism that meets the requirement; extensibility can be added later but never taken away without a
breaking change.

### Unsealed classes

- **Consider** using unsealed classes with no added virtual or protected members as a great way to
  provide inexpensive yet much appreciated extensibility. Developers inherit to add convenience
  members such as custom constructors, new methods, or overloads, and the test cost is low.

### Protected members

Protected members do not provide extensibility by themselves but make subclassing more powerful. The
name gives a false sense of security: anyone can subclass an unsealed class.

- **Consider** using protected members for advanced customization.
- **Do** treat protected members in unsealed classes as public for the purpose of security,
  documentation, and compatibility analysis.

### Events and callbacks

Callbacks let a framework call back into user code through a delegate, usually passed as a method
parameter. Events are a special case with convenient syntax and tool support.

- **Consider** using callbacks to allow users to provide custom code to be executed by the framework.
- **Consider** using events to allow users to customize the behavior of a framework without the need
  for understanding object-oriented design.
- **Do** prefer events over plain callbacks, because they are more familiar to a broader range of
  developers and are integrated with Visual Studio statement completion.
- **Avoid** using callbacks in performance-sensitive APIs.
- **Do** use the new `Func<...>`, `Action<...>`, or `Expression<...>` types instead of custom
  delegates, when defining APIs with callbacks. Delegates are for local process scenarios; expressions
  can be serialized and evaluated in a remote process.
- **Do** measure and understand performance implications of using `Expression<...>`, instead of using
  `Func<...>` and `Action<...>` delegates.
- **Do** understand that by calling a delegate, you are executing arbitrary code and that could have
  security, correctness, and compatibility repercussions.

### Virtual members

Virtual members perform better than callbacks and events but worse than non-virtual methods, can only
be changed at compile time, and are costly to design, test and maintain because any call can be
overridden in unpredictable ways.

- **Do not** make members virtual unless you have a good reason to do so and you are aware of all the
  costs related to designing, testing, and maintaining virtual members.
- **Consider** limiting extensibility to only what is absolutely necessary.
- **Do** prefer protected accessibility over public accessibility for virtual members. Public members
  should provide extensibility (if required) by calling into a protected virtual member.

### Abstractions (abstract types and interfaces)

An abstraction describes a contract without a full implementation. Getting the member set right, no
more and no fewer, is very difficult; too many abstractions hurt usability, but they power plug-ins,
inversion of control, pipelines and testability.

- **Do not** provide abstractions unless they are tested by developing several concrete
  implementations and APIs consuming the abstractions.
- **Do** choose carefully between an abstract class and an interface when designing an abstraction.
- **Consider** providing reference tests for concrete implementations of abstractions.

### Base classes for implementing abstractions

A base class in this sense sits between an abstraction and its custom implementations and provides
reusable default implementation, as `Collection<T>` and `KeyedCollection<TKey,TItem>` help implement
`IList<T>`. They add surface area and hierarchy depth, so use them only where they give users
significant value; where they help only the framework's own implementers, prefer delegation to an
internal implementation.

- **Consider** making base classes abstract even if they do not contain any abstract members. This
  clearly communicates that the class is designed solely to be inherited from.
- **Consider** placing base classes in a separate namespace from the mainline scenario types.
- **Avoid** naming base classes with a "Base" suffix if the class is intended for use in public APIs.

### Sealing

Sealing a class prevents inheriting from it; sealing a member prevents overriding it.

- **Do not** seal classes without having a good reason to do so. Not being able to think of an
  extensibility scenario is not a good reason. Good reasons: the class is static; it stores
  security-sensitive secrets in inherited protected members; it inherits many virtual members and
  sealing them individually would cost more than sealing the class; it is an attribute that needs
  fast runtime lookup.
- **Do not** declare protected or virtual members on sealed types. They cannot be called or
  overridden.
- **Consider** sealing members that you override. It shields you from the problems of virtual members
  from that point in the hierarchy on.

## Arrays

- **Do** prefer using collections over arrays in public APIs.
- **Do not** use read-only array fields. The field itself is read-only and cannot be changed, but
  elements in the array can be changed.
- **Consider** using jagged arrays instead of multidimensional arrays. A jagged array's element arrays
  can differ in size, which wastes less space for sparse data, and the CLR optimizes index operations
  on jagged arrays.

## Attributes

Attributes are annotations stored in assembly metadata and read through reflection. Required
properties are positional constructor parameters; optional properties are settable properties set with
the compiler's named-argument syntax.

- **Do** name custom attribute classes with the suffix "Attribute".
- **Do** apply the `AttributeUsageAttribute` to custom attributes.
- **Do** provide settable properties for optional arguments.
- **Do** provide get-only properties for required arguments.
- **Do** provide constructor parameters to initialize properties corresponding to required arguments.
  Each parameter should have the same name (although with different casing) as the corresponding
  property.
- **Avoid** providing constructor parameters to initialize properties corresponding to the optional
  arguments. A property should not be settable both from the constructor and from a setter.
- **Avoid** overloading custom attribute constructors. One constructor makes clear which arguments are
  required and which are optional.
- **Do** seal custom attribute classes, if possible. This makes the look-up for the attribute faster.

## Collections

Any type designed to manipulate a group of objects with a common characteristic is a collection, and
here only types implementing `IEnumerable` or `IEnumerable<T>` count as one.

- **Do not** use weakly typed collections in public APIs. Return values and parameters representing
  collection items should be the exact item type, not a base type.
- **Do not** use `ArrayList` or `List<T>` in public APIs. They are data structures for internal
  implementation. Returning `List<T>` means never receiving notifications when client code modifies the
  collection, and it exposes members such as `BinarySearch` that rarely apply.
- **Do not** use `Hashtable` or `Dictionary<TKey,TValue>` in public APIs. Use `IDictionary`,
  `IDictionary<TKey,TValue>`, or a custom type implementing one or both.
- **Do not** use `IEnumerator<T>`, `IEnumerator`, or any other type that implements either of these
  interfaces, except as the return type of a `GetEnumerator` method. Such types cannot be used with
  `foreach`.
- **Do not** implement both `IEnumerator<T>` and `IEnumerable<T>` on the same type. The same applies to
  the nongeneric interfaces.

### Collection parameters

- **Do** use the least-specialized type possible as a parameter type. Most members taking collections
  as parameters use the `IEnumerable<T>` interface.
- **Avoid** using `ICollection<T>` or `ICollection` as a parameter just to access the `Count` property.
  Take `IEnumerable<T>` and check dynamically whether the object implements `ICollection<T>`.

### Collection properties and return values

- **Do not** provide settable collection properties. Users can clear and re-add; if replacing the whole
  collection is common, provide `AddRange` on the collection.
- **Do** use `Collection<T>` or a subclass of `Collection<T>` for properties or return values
  representing read/write collections. If it does not meet some requirement, implement a custom
  collection with `IEnumerable<T>`, `ICollection<T>`, or `IList<T>`.
- **Do** use `ReadOnlyCollection<T>`, a subclass of `ReadOnlyCollection<T>`, or in rare cases
  `IEnumerable<T>` for properties or return values representing read-only collections. A custom
  read-only collection implements `ICollection<T>.IsReadOnly` to return true. Where forward-only
  iteration is the only scenario ever wanted, `IEnumerable<T>` suffices.
- **Consider** using subclasses of generic base collections instead of using the collections directly.
  This allows a better name and helper members, especially in high-level APIs.
- **Consider** returning a subclass of `Collection<T>` or `ReadOnlyCollection<T>` from very commonly
  used methods and properties, so helper methods or a different implementation can be added later.
- **Consider** using a keyed collection if the items stored in the collection have unique keys (names,
  IDs), usually by inheriting from `KeyedCollection<TKey,TItem>`. Keyed collections have larger memory
  footprints.
- **Do not** return null values from collection properties or from methods returning collections.
  Return an empty collection or an empty array instead. Null and empty should be treated the same.

### Snapshots versus live collections

A snapshot collection represents a state at some point in time (rows from a database query); a live
collection always represents the current state (the items of a combo box).

- **Do not** return snapshot collections from properties. Properties should return live collections;
  a getter must stay lightweight and a snapshot costs an O(n) copy.
- **Do** use either a snapshot collection or a live `IEnumerable<T>` (or its subtype) to represent
  collections that are volatile, that is, that can change without explicitly modifying the collection.
  Collections representing a shared resource, such as files in a directory, are volatile.

### Choosing between arrays and collections

- **Do** prefer collections over arrays. Collections give more control over contents, can evolve, and
  are more usable; cloning an array for read-only scenarios is prohibitive.
- **Consider** using arrays in low-level APIs to minimize memory consumption and maximize performance.
- **Do** use byte arrays instead of collections of bytes.
- **Do not** use arrays for properties if the property would have to return a new array (a copy of an
  internal array) every time the property getter is called.

### Implementing custom collections

- **Consider** inheriting from `Collection<T>`, `ReadOnlyCollection<T>`, or
  `KeyedCollection<TKey,TItem>` when designing new collections.
- **Do** implement `IEnumerable<T>` when designing new collections. Consider implementing
  `ICollection<T>` or even `IList<T>` where it makes sense. Follow the API pattern of `Collection<T>`
  and `ReadOnlyCollection<T>` as closely as possible: implement the same members explicitly and name
  the parameters as they do.
- **Consider** implementing nongeneric collection interfaces (`IList` and `ICollection`) if the
  collection will often be passed to APIs taking these interfaces as input.
- **Avoid** implementing collection interfaces on types with complex APIs unrelated to the concept of a
  collection.
- **Do not** inherit from nongeneric base collections such as `CollectionBase`. Use `Collection<T>`,
  `ReadOnlyCollection<T>`, and `KeyedCollection<TKey,TItem>` instead.

### Naming custom collections

Collections are created either as new data structures with their own operations and performance
(`List<T>`, `LinkedList<T>`, `Stack<T>`), mostly used internally, or as specialized collections holding
a specific set of items (`StringCollection`), mostly exposed in APIs.

- **Do** use the "Dictionary" suffix in names of abstractions implementing `IDictionary` or
  `IDictionary<TKey,TValue>`.
- **Do** use the "Collection" suffix in names of types implementing `IEnumerable` (or any of its
  descendants) and representing a list of items.
- **Do** use the appropriate data structure name for custom data structures.
- **Avoid** using any suffixes implying particular implementation, such as "LinkedList" or
  "Hashtable", in names of collection abstractions.
- **Consider** prefixing collection names with the name of the item type. A collection of `Address`
  items is `AddressCollection`; for an interface item type the "I" can be dropped, so a collection of
  `IDisposable` is `DisposableCollection`.
- **Consider** using the "ReadOnly" prefix in names of read-only collections if a corresponding
  writeable collection might be added or already exists, such as `ReadOnlyStringCollection`.

## Equality operators

- **Do not** overload one of the equality operators and not the other.
- **Do** ensure that `Object.Equals` and the equality operators have exactly the same semantics and
  similar performance characteristics. This often means overriding `Object.Equals` when the operators
  are overloaded.
- **Avoid** throwing exceptions from equality operators. Return false if one of the arguments is null
  instead of throwing `NullReferenceException`.

### Equality operators on value types

- **Do** overload the equality operators on value types, if equality is meaningful. Most languages have
  no default `==` for value types.

### Equality operators on reference types

- **Avoid** overloading equality operators on mutable reference types. The built-in operators implement
  reference equality, and developers are surprised when that changes to value equality. Immutable
  reference types make the difference much harder to notice.
- **Avoid** overloading equality operators on reference types if the implementation would be
  significantly slower than that of reference equality.

## Dispose pattern

Managed memory is released by the garbage collector; every other system resource (handles, database
connections, unmanaged buffers) must be released explicitly. A finalizer runs at an undetermined time
after the object becomes eligible for collection and delays reclaiming its memory to a later collection,
so it is a poor fit for scarce or costly resources. `System.IDisposable` gives the developer a manual,
deterministic release, and `GC.SuppressFinalize` tells the collector a disposed object no longer needs
finalizing. The pattern standardizes how `Dispose` and `Finalize` share their cleanup code.

- **Do** implement the Basic Dispose Pattern on types containing instances of disposable types. A type
  responsible for the lifetime of other disposable objects gives its users a way to dispose them too.
- **Do** implement the Basic Dispose Pattern and provide a finalizer on types holding resources that
  need to be freed explicitly and that do not have finalizers, such as unmanaged memory buffers.
- **Consider** implementing the Basic Dispose Pattern on classes that themselves do not hold unmanaged
  resources or disposable objects but are likely to have subtypes that do, as `System.IO.Stream` does.

### Basic Dispose Pattern

Implement `System.IDisposable` and declare a `Dispose(bool)` method holding all cleanup logic shared
between `Dispose` and the optional finalizer.

```csharp
public class DisposableResourceHolder : IDisposable {
    private SafeHandle resource; // handle to a resource
    public DisposableResourceHolder() {
        this.resource = ... // allocates the resource
    }
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            if (resource!= null) resource.Dispose();
        }
    }
}
```

The `disposing` parameter says whether the call came from `IDisposable.Dispose` (true) or from the
finalizer (false). Other reference objects may be touched only when it is true, because during
finalization they may already have been finalized in an unpredictable order. A class whose base already
implements the pattern only overrides `Dispose(bool)`.

- **Do** declare a `protected virtual void Dispose(bool disposing)` method to centralize all logic
  related to releasing unmanaged resources. It is called from both the finalizer and
  `IDisposable.Dispose`, with `false` from the finalizer.
- **Do** implement the `IDisposable` interface by simply calling `Dispose(true)` followed by
  `GC.SuppressFinalize(this)`. The call to `SuppressFinalize` should only occur if `Dispose(true)`
  executes successfully.
- **Do not** make the parameterless `Dispose` method virtual. `Dispose(bool)` is the one subclasses
  override.

```csharp
// bad design
public class DisposableResourceHolder : IDisposable {
    public virtual void Dispose() { ... }
    protected virtual void Dispose(bool disposing) { ... }
}
// good design
public class DisposableResourceHolder : IDisposable {
    public void Dispose() { ... }
    protected virtual void Dispose(bool disposing) { ... }
}
```

- **Do not** declare any overloads of the `Dispose` method other than `Dispose()` and
  `Dispose(bool)`. Treat `Dispose` as a reserved word.
- **Do** allow the `Dispose(bool)` method to be called more than once. The method might choose to do
  nothing after the first call.

```csharp
public class DisposableResourceHolder : IDisposable {
    bool disposed = false;
    protected virtual void Dispose(bool disposing) {
        if (disposed) return;
        // cleanup
        ...
        disposed = true;
    }
}
```

- **Avoid** throwing an exception from within `Dispose(bool)` except under critical situations where
  the containing process has been corrupted (leaks, inconsistent shared state). Users expect `Dispose`
  not to throw; a throwing `Dispose` stops the rest of a `finally` block. Never throw when `disposing`
  is false, because that terminates the process inside a finalizer.
- **Do** throw an `ObjectDisposedException` from any member that cannot be used after the object has
  been disposed of.

```csharp
public class DisposableResourceHolder : IDisposable {
    bool disposed = false;
    SafeHandle resource; // handle to a resource
    public void DoSomething() {
        if (disposed) throw new ObjectDisposedException(...);
        // now call some native methods using the resource
        ...
    }
    protected virtual void Dispose(bool disposing) {
        if (disposed) return;
        // cleanup
        ...
        disposed = true;
    }
}
```

- **Consider** providing method `Close()`, in addition to the `Dispose()`, if close is standard
  terminology in the area. Make `Close` identical to `Dispose` and consider implementing
  `IDisposable.Dispose` explicitly.

```csharp
public class Stream : IDisposable {
    IDisposable.Dispose() {
        Close();
    }
    public void Close() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
```

### Finalizable types

Finalizable types extend the basic pattern by overriding the finalizer and providing a finalization
code path in `Dispose(bool)`. The guidelines apply to any code run from a finalizer, which in the basic
pattern means the `Dispose(bool)` logic when `disposing` is false. If the base class is already
finalizable and implements the pattern, do not override `Finalize` again; override `Dispose(bool)`.

```csharp
public class ComplexResourceHolder : IDisposable {
    private IntPtr buffer; // unmanaged memory buffer
    private SafeHandle resource; // disposable handle to a resource
    public ComplexResourceHolder() {
        this.buffer = ... // allocates memory
        this.resource = ... // allocates the resource
    }
    protected virtual void Dispose(bool disposing) {
        ReleaseBuffer(buffer); // release unmanaged memory
        if (disposing) { // release other disposable objects
            if (resource!= null) resource.Dispose();
        }
    }
    ~ComplexResourceHolder() {
        Dispose(false);
    }
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
```

- **Avoid** making types finalizable. Finalizers cost performance and complexity; prefer resource
  wrappers such as `SafeHandle`, which clean up their own resource and make a finalizer unnecessary.
- **Do not** make value types finalizable. Only reference types are finalized; the C# and C++ compilers
  enforce this.
- **Do** make a type finalizable if the type is responsible for releasing an unmanaged resource that
  does not have its own finalizer. The finalizer simply calls `Dispose(false)`.
- **Do** implement the Basic Dispose Pattern on every finalizable type, so users can deterministically
  clean up the same resources the finalizer is responsible for.
- **Do not** access any finalizable objects in the finalizer code path, because there is significant
  risk that they will have already been finalized. Finalizers run in a random order. Objects in static
  variables are collected during application domain unload or process exit, so touching them is unsafe
  once `Environment.HasShutdownStarted` returns true.
- **Do** make your `Finalize` method protected. The C#, C++ and VB.NET compilers enforce this.
- **Do not** let exceptions escape from the finalizer logic, except for system-critical failures. An
  exception from a finalizer shuts down the entire process and stops other finalizers from running.
- **Consider** creating and using a critical finalizable object (a type whose hierarchy contains
  `CriticalFinalizerObject`) for situations in which a finalizer absolutely must execute even in the
  face of forced application domain unloads and thread aborts.

## Library-only chapters, summarized

**Serialization.** The source recommends thinking about serialization when designing new types,
preferring Data Contract Serialization for general persistence over XML Serialization or Runtime
Serialization, giving `DataMember` properties both a getter and a setter, using serialization callbacks
where constructor logic is needed after deserialization, and treating data member names, types and
order as a compatibility contract. This repository serializes through ASP.NET Core's JSON pipeline and
Entity Framework, so none of the attribute-level rules apply here.

**System.Xml usage.** Expose XML through `XmlReader`, `IXPathNavigable` or `XNode` subtypes, never
`XmlNode` or `XmlDocument`, and never subclass `XmlDocument`. This repository handles no XML.

**Dependency properties.** WPF-specific: derive from `DependencyObject`, back each property with a
static `...Property` field, keep accessors to plain `GetValue` and `SetValue`, and put defaults,
validation, change notification and coercion into the registration metadata rather than the accessors.
The desktop shell is Avalonia and the rule set does not transfer.
