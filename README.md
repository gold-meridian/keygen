# keygen

---

**keygen** is a source generator for automatically relating objects to extended data through `ConditionalWeakTable`s using C# 14 extension members.

It is a purely compile-time (dev-time) dependency and only serves to produce workable code that may be trivially consumed within your codebase and by other API consumers.

```
dotnet add package GoldMeridian.KeyGen
```

## example

Given the class:

```cs
public sealed class MyClass
{
    public int SomeData { get; }
}
```

You may want to relate additional information to an instance of `MyClass`.  This is a common use-case and one officially supported by .NET; you would use a [`ConditionalWeakTable<TKey, TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2) to bind additional data to a reference.

`ConditionalWeakTable` uses [`WeakReference`](https://learn.microsoft.com/en-us/dotnet/api/system.weakreference)s to point to a given instance without capturing a direct reference which would thereby prevent garbage collection.

This source generator automates the scaffolding necessary to conveniently access additional data by generating extension members.

A piece of extended data may look like:

```cs
public sealed class MyClassData
{
    public string MoreData { get; }
}
```

Annotating the class `MyClassData` with `[ExtensionDataFor<MyClass>]` would then allow the source generator to automatically generate the `ConditionalWeakTable` reference.

```cs
// Generates an extension property for MyClass named "Data" (omits the start of
// the string if it matches the type name).
[ExtensionDataFor<MyClass>]
public sealed class MyClassData;

// A name may also be manually specified.  This would generate a propery named
// "TheData".
[ExtensionDataFor<MyClass>("TheData")]
public sealed class MyClassData;

// If the type doesn't start with the class name, then it'll just use the entire
// type name.  In this case, "Data".
[ExtensionDataFor<MyClass>]
public sealed class Data;

// It's also possible and explicitly intended for you to be able to link a single
// class to multiple types:
[ExtensionDataFor<MyClass1>]
[ExtensionDataFor<MyClass2>]
public sealed class Data;
```

The shape of the generated code would look something like this:

```cs
#nullable enable

namespace Namespace.Of.Extension.Type;

// Not always `public`; matches the accessibility of the Extension class.
public static class KeyNameDataNameExtensions
{
    extension(KeyName)
    {
        public static ConditionalWeakTable<KeyName, DataName> GetDataNameTable();
    }

    extension(KeyName value)
    {
        public ValueName? PropertyName { get; set; }
    }
}
```
