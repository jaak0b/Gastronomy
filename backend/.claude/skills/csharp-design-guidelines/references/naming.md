# Naming guidelines

Condensed from the Naming chapter of the Framework Design Guidelines: capitalization, general naming,
assemblies, namespaces, classes, structs and interfaces, type members, parameters and resources.

## Capitalization conventions

Capitalize the first letter of each word in an identifier; never separate words with underscores.
PascalCasing capitalizes every word including acronyms longer than two letters (`PropertyDescriptor`,
`HtmlTag`); a two-letter acronym keeps both letters in capitals (`IOStream`). camelCasing capitalizes
every word but the first, and a leading two-letter acronym is all lowercase (`ioStream`, `htmlTag`).

- **Do** use PascalCasing for all public member, type, and namespace names consisting of multiple words.
- **Do** use camelCasing for parameter names.

| Identifier | Casing | Example |
|---|---|---|
| Namespace | Pascal | `namespace System.Security { ... }` |
| Type | Pascal | `public class StreamReader { ... }` |
| Interface | Pascal | `public interface IEnumerable { ... }` |
| Method | Pascal | `public virtual string ToString();` |
| Property | Pascal | `public int Length { get; }` |
| Event | Pascal | `public event EventHandler Exited;` |
| Field | Pascal | `public static readonly TimeSpan InfiniteTimeout;` |
| Enum value | Pascal | `public enum FileMode { Append, ... }` |
| Parameter | Camel | `public static int ToInt32(string value);` |

### Compound words and common terms

- **Do not** capitalize each word in so-called closed-form compound words. Treat a closed-form
  compound (a compound written as one word, such as endpoint) as a single word, and use a current
  dictionary to decide whether a compound is closed.

| Pascal | Camel | Not |
|---|---|---|
| BitFlag | bitFlag | Bitflag |
| Callback | callback | CallBack |
| Canceled | canceled | Cancelled |
| DoNot | doNot | Don't |
| Email | email | EMail |
| Endpoint | endpoint | EndPoint |
| FileName | fileName | Filename |
| Gridline | gridline | GridLine |
| Hashtable | hashtable | HashTable |
| Id | id | ID |
| Indexes | indexes | Indices |
| LogOff | logOff | LogOut |
| LogOn | logOn | LogIn |
| Metadata | metadata | MetaData, metaData |
| Multipanel | multipanel | MultiPanel |
| Multiview | multiview | MultiView |
| Namespace | namespace | NameSpace |
| Ok | ok | OK |
| Pi | pi | PI |
| Placeholder | placeholder | PlaceHolder |
| SignIn | signIn | SignOn |
| SignOut | signOut | SignOff |
| UserName | userName | Username |
| WhiteSpace | whiteSpace | Whitespace |
| Writable | writable | Writeable |

### Case sensitivity

- **Do not** assume that all programming languages are case sensitive. They are not. Names cannot
  differ by case alone.

## General naming conventions

### Word choice

- **Do** choose easily readable identifier names. `HorizontalAlignment` reads better than
  `AlignmentHorizontal`.
- **Do** favor readability over brevity. `CanScrollHorizontally` is better than `ScrollableX`.
- **Do not** use underscores, hyphens, or any other nonalphanumeric characters.
- **Do not** use Hungarian notation.
- **Avoid** using identifiers that conflict with keywords of widely used programming languages. C#
  offers `@` as an escape, but a member that needs escaping is harder to use than one that does not.

### Abbreviations and acronyms

- **Do not** use abbreviations or contractions as part of identifier names. Use `GetWindow`, not
  `GetWin`.
- **Do not** use any acronyms that are not widely accepted, and even if they are, only when necessary.

### Language-specific names

- **Do** use semantically interesting names rather than language-specific keywords for type names.
  `GetLength` is a better name than `GetInt`.
- **Do** use a generic CLR type name, rather than a language-specific name, in the rare cases when an
  identifier has no semantic meaning beyond its type. A method converting to `Int64` is `ToInt64`,
  not `ToLong`.

| C# | CLR |
|---|---|
| sbyte | SByte |
| byte | Byte |
| short | Int16 |
| ushort | UInt16 |
| int | Int32 |
| uint | UInt32 |
| long | Int64 |
| ulong | UInt64 |
| float | Single |
| double | Double |
| bool | Boolean |
| char | Char |
| string | String |
| object | Object |

- **Do** use a common name, such as `value` or `item`, rather than repeating the type name, in the
  rare cases when an identifier has no semantic meaning and the type of the parameter is not important.

### Naming new versions of existing APIs

- **Do** use a name similar to the old API when creating new versions of an existing API.
- **Do** prefer adding a suffix rather than a prefix to indicate a new version of an existing API, so
  the two sort next to each other.
- **Consider** using a brand new, but meaningful identifier, instead of adding a suffix or a prefix.
- **Do** use a numeric suffix to indicate a new version of an existing API, particularly if the
  existing name is the only name that makes sense (an industry standard) and no meaningful suffix fits.
- **Do not** use the "Ex" (or a similar) suffix for an identifier to distinguish it from an earlier
  version of the same API.
- **Do** use the "64" suffix when introducing versions of APIs that operate on a 64-bit integer
  instead of a 32-bit integer, and only when the 32-bit API already exists.

## Names of assemblies and DLLs

- **Do** choose names for your assembly DLLs that suggest large chunks of functionality, such as
  `System.Data`. A good rule of thumb is the common prefix of the namespaces the assembly contains.
- **Consider** naming DLLs according to the pattern `<Company>.<Component>.dll`, where `<Component>`
  contains one or more dot-separated clauses, for example `Litware.Controls.dll`.

## Names of namespaces

The general template is `<Company>.(<Product>|<Technology>)[.<Feature>][.<Subnamespace>]`, for
example `Fabrikam.Math` or `Litware.Security`.

- **Do** prefix namespace names with a company name to prevent namespaces from different companies
  from having the same name.
- **Do** use a stable, version-independent product name at the second level of a namespace name.
- **Do not** use organizational hierarchies as the basis for names in namespace hierarchies, because
  group names within corporations tend to be short-lived. Organize around groups of related
  technologies.
- **Do** use PascalCasing, and separate namespace components with periods. A brand with nontraditional
  casing keeps the casing of the brand.
- **Consider** using plural namespace names where appropriate: `System.Collections`, not
  `System.Collection`. Brand names and acronyms are exceptions (`System.IO`).
- **Do not** use the same name for a namespace and a type in that namespace, such as a `Debug`
  namespace containing a `Debug` class. Several compilers require such types to be fully qualified.

### Namespaces and type name conflicts

- **Do not** introduce generic type names such as `Element`, `Node`, `Log`, and `Message`. Qualify
  them: `FormElement`, `XmlNode`, `EventLog`, `SoapMessage`.
- **Do not** give the same name to types in namespaces within a single application model. Do not add
  a `Page` to `System.Web.UI.Adapters` when `System.Web.UI` already has one.
- **Do not** give types names that would conflict with any type in the Core namespaces (`System`,
  `System.IO`, `System.Xml`, `System.Net` and the rest). Never use `Stream` as a type name.
- **Do not** assign type names that would conflict with other types within a single technology.
- **Do not** introduce type name conflicts between types in technology namespaces and an application
  model namespace, unless the technology is not intended to be used with the application model.

## Names of classes, structs, and interfaces

- **Do** name classes and structs with nouns or noun phrases, using PascalCasing. This distinguishes
  type names from methods, which are named with verb phrases.
- **Do** name interfaces with adjective phrases, or occasionally with nouns or noun phrases. A noun
  name used often might indicate that the type should be an abstract class, not an interface.
- **Do not** give class names a prefix (for example "C").
- **Consider** ending the name of derived classes with the name of the base class:
  `ArgumentOutOfRangeException` is a kind of `Exception`, `SerializableAttribute` a kind of
  `Attribute`. Apply judgment: `Button` is a kind of `Control` without carrying the word.
- **Do** prefix interface names with the letter I, to indicate that the type is an interface:
  `IComponent`, `ICustomAttributeProvider`, `IPersistable`.
- **Do** ensure that the names differ only by the "I" prefix on the interface name when you are
  defining a class and interface pair where the class is a standard implementation of the interface.

### Names of generic type parameters

- **Do** name generic type parameters with descriptive names unless a single-letter name is completely
  self-explanatory and a descriptive name would not add value.
- **Consider** using `T` as the type parameter name for types with one single-letter type parameter.

```csharp
public int IComparer<T> { ... }
public delegate bool Predicate<T>(T item);
public struct Nullable<T> where T:struct { ... }
```

- **Do** prefix descriptive type parameter names with T.
- **Consider** indicating constraints placed on a type parameter in the name of the parameter. A
  parameter constrained to `ISession` might be called `TSession`.

```csharp
public interface ISessionChannel<TSession> where TSession : ISession {
    TSession Session { get; }
}
```

### Names of common types

| Base type | Guideline |
|---|---|
| `System.Attribute` | **Do** add the suffix "Attribute" to names of custom attribute classes. |
| `System.Delegate` | **Do** add the suffix "EventHandler" to names of delegates that are used in events. **Do** add the suffix "Callback" to names of delegates other than those used as event handlers. **Do not** add the suffix "Delegate" to a delegate. |
| `System.EventArgs` | **Do** add the suffix "EventArgs". |
| `System.Enum` | **Do not** derive from this class; use the `enum` keyword instead. **Do not** add the suffix "Enum" or "Flag". |
| `System.Exception` | **Do** add the suffix "Exception". |
| `IDictionary`, `IDictionary<TKey,TValue>` | **Do** add the suffix "Dictionary". This takes precedence over the collection guideline below. |
| `IEnumerable`, `ICollection`, `IList` and their generic forms | **Do** add the suffix "Collection". |
| `System.IO.Stream` | **Do** add the suffix "Stream". |
| `CodeAccessPermission`, `IPermission` | **Do** add the suffix "Permission". |

### Naming enumerations

- **Do** use a singular type name for an enumeration unless its values are bit fields.
- **Do** use a plural type name for an enumeration with bit fields as values, also called flags enum.
- **Do not** use an "Enum" suffix in enum type names.
- **Do not** use "Flag" or "Flags" suffixes in enum type names.
- **Do not** use a prefix on enumeration value names (for example "ad" for ADO enums, "rtf" for rich
  text enums).

## Names of type members

### Methods

- **Do** give methods names that are verbs or verb phrases.

```csharp
public class String {
    public int CompareTo(...);
    public string[] Split(...);
    public string Trim();
}
```

### Properties

- **Do** name properties using a noun, noun phrase, or adjective.
- **Do not** have properties that match the name of "Get" methods. The pattern below usually means
  the property should really be a method.

```csharp
public string TextWriter { get {...} set {...} }
public string GetTextWriter(int value) { ... }
```

- **Do** name collection properties with a plural phrase describing the items in the collection instead
  of using a singular phrase followed by "List" or "Collection".
- **Do** name Boolean properties with an affirmative phrase (`CanSeek` instead of `CantSeek`).
  Optionally, you can also prefix Boolean properties with "Is", "Can", or "Has", but only where it
  adds value.
- **Consider** giving a property the same name as its type.

```csharp
public enum Color {...}
public class Control {
    public Color Color { get {...} set {...} }
}
```

### Events

- **Do** name events with a verb or a verb phrase: `Clicked`, `Painting`, `DroppedDown`.
- **Do** give events names with a concept of before and after, using the present and past tenses. A
  close event raised before a window is closed is `Closing`; one raised after is `Closed`.
- **Do not** use "Before" or "After" prefixes or postfixes to indicate pre- and post-events. Use present
  and past tenses as just described.
- **Do** name event handlers (delegates used as types of events) with the "EventHandler" suffix.

```csharp
public delegate void ClickedEventHandler(object sender, ClickedEventArgs e);
```

- **Do** use two parameters named `sender` and `e` in event handlers. The sender is typically of type
  `object`, even if a more specific type is possible.
- **Do** name event argument classes with the "EventArgs" suffix.

### Fields

These guidelines apply to static public and protected fields. Internal and private fields are not
covered, and public or protected instance fields are not allowed by the member design guidelines.

- **Do** use PascalCasing in field names.
- **Do** name fields using a noun, noun phrase, or adjective.
- **Do not** use a prefix for field names, such as "g_" or "s_" to indicate static fields.

## Naming parameters

- **Do** use camelCasing in parameter names.
- **Do** use descriptive parameter names.
- **Consider** using names based on a parameter's meaning rather than the parameter's type.

### Operator overload parameters

- **Do** use `left` and `right` for binary operator overload parameter names if there is no meaning to
  the parameters.
- **Do** use `value` for unary operator overload parameter names if there is no meaning to the
  parameters.
- **Consider** meaningful names for operator overload parameters if doing so adds significant value.
- **Do not** use abbreviations or numeric indices for operator overload parameter names.

## Naming resources

Localizable resources can be referenced as if they were properties, so their naming follows property
guidelines.

- **Do** use PascalCasing in resource keys.
- **Do** provide descriptive rather than short identifiers.
- **Do not** use language-specific keywords of the main CLR languages.
- **Do** use only alphanumeric characters and underscores in naming resources.
- **Do** use the following naming convention for exception message resources: the exception type name
  plus a short identifier of the exception, such as `ArgumentExceptionIllegalCharacters`,
  `ArgumentExceptionInvalidName`, `ArgumentExceptionFileNameIsMalformed`.
