# Design guidelines for exceptions

Condensed from the Exceptions chapter of the Framework Design Guidelines: throwing, standard exception
types, and exceptions and performance.

Exception handling has many advantages over return-value-based error reporting. An execution failure
occurs whenever a member cannot do what its name implies: if `OpenFile` cannot return an opened file
handle, that is an execution failure. In the Framework, exceptions are used for all error conditions,
including execution errors, not only usage errors such as division by zero.

## Exception throwing

- **Do not** return error codes. Exceptions are the primary means of reporting errors.
- **Do** report execution failures by throwing exceptions.
- **Consider** terminating the process by calling `System.Environment.FailFast` instead of throwing an
  exception if your code encounters a situation where it is unsafe for further execution.
- **Do not** use exceptions for the normal flow of control, if possible. Except for system failures
  and operations with potential race conditions, design APIs so users can write code that does not
  throw, for example by providing a way to check preconditions before calling a member. The checking
  member is called a tester and the working member a doer; where that costs too much, use the Try-Parse
  pattern below.
- **Consider** the performance implications of throwing exceptions. Throw rates above 100 per second
  are likely to noticeably impact the performance of most applications.
- **Do** document all exceptions thrown by publicly callable members because of a violation of the
  member contract (rather than a system failure) and treat them as part of your contract. Their type
  should not change and new ones should not be added between versions.
- **Do not** have public members that can either throw or not based on some option.
- **Do not** have public members that return exceptions as the return value or an `out` parameter.
  Returning exceptions defeats the benefits of exception-based error reporting.
- **Consider** using exception builder methods. The same exception is often thrown from different
  places; a helper that creates and initializes it avoids code bloat, and a member whose throw lives
  in the builder can still be inlined.
- **Do not** throw exceptions from exception filter blocks. The CLR catches it, the filter returns
  false, and the outcome is indistinguishable from the filter returning false itself.
- **Avoid** explicitly throwing exceptions from `finally` blocks. Implicitly thrown exceptions from
  called methods are acceptable.

## Using standard exception types

### Exception and SystemException

- **Do not** throw `System.Exception` or `System.SystemException`.
- **Do not** catch `System.Exception` or `System.SystemException` in framework code, unless you intend
  to rethrow.
- **Avoid** catching `System.Exception` or `System.SystemException`, except in top-level exception
  handlers.

### ApplicationException

- **Do not** throw or derive from `ApplicationException`.

### InvalidOperationException

- **Do** throw an `InvalidOperationException` if the object is in an inappropriate state.

### ArgumentException, ArgumentNullException, and ArgumentOutOfRangeException

- **Do** throw `ArgumentException` or one of its subtypes if bad arguments are passed to a member.
  Prefer the most derived exception type, if applicable.
- **Do** set the `ParamName` property when throwing one of the subclasses of `ArgumentException`. The
  constructor overloads accept it.
- **Do** use `value` for the name of the implicit value parameter of property setters.

### NullReferenceException, IndexOutOfRangeException, and AccessViolationException

- **Do not** allow publicly callable APIs to explicitly or implicitly throw `NullReferenceException`,
  `AccessViolationException`, or `IndexOutOfRangeException`. These exceptions are reserved and thrown
  by the execution engine and in most cases indicate a bug. Check arguments so they never happen;
  letting them escape exposes implementation details that might change.

### StackOverflowException

- **Do not** explicitly throw `StackOverflowException`. Only the CLR throws it.
- **Do not** catch `StackOverflowException`. Managed code cannot stay consistent through an arbitrary
  stack overflow.

### OutOfMemoryException

- **Do not** explicitly throw `OutOfMemoryException`. Only the CLR infrastructure throws it.

### ComException, SEHException, and ExecutionEngineException

- **Do not** explicitly throw `COMException`, `ExecutionEngineException`, and `SEHException`. Only the
  CLR infrastructure throws them.

## Exceptions and performance

A member that throws can be orders of magnitude slower, but good performance is possible while still
refusing error codes.

- **Do not** use error codes because of concerns that exceptions might affect performance negatively.

### Tester-Doer pattern

`ICollection<T>.Add` throws if the collection is read-only. Where the call is expected to fail often,
test first.

```csharp
ICollection<int> numbers = ...
...
if (!numbers.IsReadOnly)
{
    numbers.Add(1);
}
```

The member that tests a condition (`IsReadOnly`) is the tester; the member that performs the
potentially throwing operation (`Add`) is the doer.

- **Consider** the Tester-Doer pattern for members that might throw exceptions in common scenarios to
  avoid performance problems related to exceptions.

### Try-Parse pattern

For extremely performance-sensitive APIs, make a well-defined test case part of the member semantics.
`DateTime.Parse` throws when parsing fails; `DateTime.TryParse` returns false and delivers the result
through an `out` parameter.

```csharp
public struct DateTime
{
    public static DateTime Parse(string dateTime)
    {
        ...
    }
    public static bool TryParse(string dateTime, out DateTime result)
    {
        ...
    }
}
```

Define the try functionality in strict terms: if the member fails for any reason other than the
well-defined try, it must still throw a corresponding exception.

- **Consider** the Try-Parse pattern for members that might throw exceptions in common scenarios to
  avoid performance problems related to exceptions.
- **Do** use the prefix "Try" and Boolean return type for methods implementing this pattern.
- **Do** provide an exception-throwing member for each member using the Try-Parse pattern.
