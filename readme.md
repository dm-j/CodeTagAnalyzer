# CodeTag Analyzer
**CodeTag** is a Roslyn-based analyzer that allows developers to enforce the use of specific tags on their code, thereby allowing developers to trace references throughout a codebase.

`ObsoleteAttribute` propagates through a codebase by following references, issuing Warnings or Errors until you either remove the reference or tag the containing method or property with `ObsoleteAttribute`. 

**CodeTag** does the same thing. Apply `DefineCodeTagAttribute` to a property, method, or constructor, and anything which uses it will require a `CodeTagAttribute` with a `string` identifier to match it. If you do not provide a `string` identifier value to the `DefineCodeTagAttribute` it will attempt to generate something plausible from the code structure.

The original use case is that I needed a way to ensure that any code that references (no matter how indirectly) an EF navigation property was distinctively marked, so I could make sure it was eagerly loaded correctly.

## Features
### CodeTag Attributes
* `CodeTag.Common.DefineCodeTagAttribute` marks a method, property, or constructor as "tagged". Optionally provide a `string` identifier to the constructor to use a custom identifier.
* `CodeTag.Common.CodeTagAttribute` acknowledges a method, property, or constructor as "tagged" and deactivates diagnostics related to this particular tag identifier for this element.
### CodeTag Analyzer
* Analyzer that checks your code for the correct use of CodeTags and provides diagnostic feedback.
### CodeTag CodeFix
* Offers quick fixes for certain identified issues, streamlining the correction process.
## Getting Started
Install the NuGet package to your C# project using your package management process of choice

## Usage

### Simple Tagging
In CodeTag's most basic use-case, you can tag methods, properties, or constructors to trace their usage across your codebase. Let's dive into a few examples:

#### Default Tag Generation:

```csharp
using CodeTag.Common;

namespace Test
{
    public class Beta
    {
        [DefineCodeTag] // This will generate a default tag
        public Gamma Charlie { get; set; } = default!;

        [CodeTag("Test.Beta.Charlie")] // Using the generated tag
        public void Bar()
        {
            Charlie.Foo();
        }
    }

    public class Gamma 
    { 
        public void Foo() { }
    }
}
```
* Property `Beta.Charlie` is tagged with the `DefineCodeTag` attribute, generating the default identifier `Test.Beta.Charlie` 
  * Default identifiers are generated from the namespace, enclosing types, and element name.
* Method `Beta.Bar()` has a reference to `Charlie`, so must be tagged with a matching `CodeTag`.

Methods can also be tagged using the `DefineCodeTag` attribute. Once tagged, any other method, property, or constructor that references the tagged method requires its own `CodeTag` attribute with the matching identifier.


``` csharp
using CodeTag.Common;

namespace Test
{
    public class Beta
    {
        // Tagging a method with default identifier generation
        [DefineCodeTag]
        public int Foo()
        {
            return 1;
        }

        // Referencing the tagged method requires a matching CodeTag
        [CodeTag("Test.Beta.Foo")]
        public int Bar()
        {
            return Foo();
        }

        // Properties that use tagged methods and properties also require matching CodeTag attributes
        [CodeTag("Test.Beta.Foo")]
        public int Alice => Bar(); 
    }
}
```
* The method `Beta.Foo()` is tagged using the `DefineCodeTag` attribute. Since there is no string argument provided, it generates the identifier for this tag from the method name, enclosing classes, and namespace, resulting in `Test.Beta.Foo`.
* The method `Beta.Bar()` references `Beta.Foo()`, and as a result, requires its own `CodeTag` attribute with the identifier `Test.Beta.Foo`.
* The property `Beta.Alice` references `Beta.Bar()`, and as a result, _also_ requires its own `CodeTag` attribute with the identifier `Test.Beta.Foo`, because `Beta.Bar()` references `Beta.Foo()`. 


#### Custom Tag Identifier:

```csharp
using CodeTag.Common;

namespace Test
{
    public class Beta
    {
        [DefineCodeTag("Beep!")] // Custom tag identifier
        public Gamma Charlie { get; set; } = default!;

        [CodeTag("Beep!")] // Using the custom tag
        public void Bar()
        {
            Charlie.Foo();
        }
    }

    public class Gamma 
    { 
        public void Foo() { }
    }
}
```
* The `DefineCodeTag` attribute accepts a custom `string` as identifiers.
* `Beta.Charlie` uses the custom tag `Beep!`.
* So `Beta.Bar()` _also_ requires the custom tag `Beep!`.

#### Stacking Tags
When a method, property, or constructor references multiple tagged elements, it may be required to stack multiple `CodeTag` attributes to address each individual tag. This ensures that all dependencies of a function are properly tracked.

``` csharp
using CodeTag.Common;

namespace Test
{
    public class Alpha
    {
        [DefineCodeTag("Beep!")] // Custom tag for Brian property
        public Beta Brian { get; set; } = default!;

        // Applying multiple tags
        [CodeTag("Beep!")]
        [CodeTag("Test.Gamma.Foo")]
        [CodeTag("Test.Beta.Charlie")]
        public void Baz()
        {
            Brian.Bar(); // This method references both "Test.Beta.Charlie" and "Test.Gamma.Foo"
        }
    }

    public class Beta
    {
        [DefineCodeTag] // Default tag for Charlie property
        public Gamma Charlie { get; set; } = default!;

        // Stacking required tags
        [CodeTag("Test.Beta.Charlie")]
        [CodeTag("Test.Gamma.Foo")]
        public void Bar()
        {
            Charlie.Foo(); // This method references a tagged element
        }
    }

    public class Gamma 
    { 
        [DefineCodeTag] // Default tag for Foo method
        public void Foo() { }
    }
}
```

* Method `Alpha.Baz()` references `Beta.Bar()`, which in turn references `Gamma.Foo()`.
* Each of these methods and properties has their own tags, and due to this hierarchy of calls, the method `Alpha.Baz()` requires all three tags (`Beep!`, `Test.Gamma.Foo`, and `Test.Beta.Charlie`).
* This demonstrates the ability to stack multiple `CodeTag` attributes on a single element to address all its references.

**Remember, stacking is essential when a single method, property, or constructor interacts with multiple tagged elements. It ensures that all related tags are acknowledged, keeping your codebase traceable.**

### Diagnostic Errors
* [CT001: Missing CodeTag](#ct001-missing-codetag)
* [CT002: Unnecessary CodeTag](#ct002-unnecessary-codetag)
* [CT003: Duplicate CodeTag](#ct003-duplicate-codetag)


#### CT001 Missing CodeTag

An element which references an element which is tagged with a CodeTag requires a `CodeTag` attribute with a matching identifier. 

If it does not, Error `CT001 Missing CodeTag` will be issued.

``` csharp
using CodeTag.Common;
namespace Test
{
    public class Beta
    {
        [DefineCodeTag] // This will generate a default tag
        public Gamma Charlie { get; set; } = default!;

        public void Bar() // Error CT001 Missing CodeTag: Method or property 'Bar' must have [CodeTag("Test.Beta.Charlie")]
        {
            Charlie.Foo();
        }
    }

    public class Gamma 
    { 
        public void Foo() { }
    }
}
```

To resolve this Error, either add the CodeTag or remove it from the referenced elements.

#### CT002 Unnecessary CodeTag

The analyzer can detect and highlight instances where a CodeTag attribute is used, but the corresponding reference that necessitates the tag is absent.

``` csharp
using CodeTag.Common;

namespace Test
{
    public class Beta
    {
        public Gamma Charlie { get; set; } = default!;

        // This CodeTag is unnecessary because there's no reference to a tagged element.
        [CodeTag("Test.Beta.Charlie")]
        public void Bar()
        {
            Charlie.Foo();
        }
    }

    public class Gamma 
    { 
        public void Foo() { }
    }
}
```
* `Beta.Bar()` has been tagged with the CodeTag `Test.Beta.Charlie`, but `Beta.Charlie` isn't tagged with a `DefineCodeTag` attribute.
* The analyzer will issue a Warning-level diagnostic `CT002 (Unnecessary CodeTag)`

To resolve this warning, you have a couple of options:

* Remove the unnecessary CodeTag attribute.

```csharp 
public void Bar()
{
    Charlie.Foo();
}
```
* If the intent was to tag Charlie, then add a `DefineCodeTag` attribute

```csharp
[DefineCodeTag]
public Gamma Charlie { get; set; } = default!;
```
By addressing unnecessary tags, you ensure that your code tagging system remains relevant, concise, and meaningful.

#### CT003 Duplicate CodeTag

Applying the same CodeTag more than once to a single code element is redundant and can lead to confusion.

The analyzer detects and reports these redundancies to help maintain the clarity of your code tagging system.

``` csharp
Copy code
using CodeTag.Common;

namespace Test
{
    public class Beta
    {
        [DefineCodeTag]
        public Gamma Charlie { get; set; } = default!;

        // Applying the same CodeTag twice is redundant
        // CT003 (Duplicate CodeTag): Duplicate CodeTag [CodeTag("Test.Beta.Charlie")] on 'Bar'
        [CodeTag("Test.Beta.Charlie")]
        [CodeTag("Test.Beta.Charlie")]
        public void Bar()
        {
            Charlie.Foo();
        }
    }

    public class Gamma 
    { 
        public void Foo() { }
    }
}
```
* `Beta.Bar()` references `Beta.Charlie` and is therefore tagged with the corresponding `CodeTag`. 
* The `CodeTag` attribute has been applied twice with the same identifier, which is unnecessary.
* Such code will be flagged with an Error-level diagnostic `CT003`.


To resolve this, simply remove the redundant CodeTag attribute:

``` csharp
[CodeTag("Test.Beta.Charlie")]
public void Bar()
{
    Charlie.Foo();
}
```
By ensuring that each code element has unique tags, you can maintain a clean and effective code tagging system.

### Viewing Diagnostics
Once the tags are in place, the analyzer will automatically check your code in the background. If there are any issues, they will be displayed in the Error List window of your IDE. Issues will be flagged with appropriate markings (red squiggles).

### Applying Fixes
If an issue has a fix provided by the CodeFix Provider, you can quickly apply the recommended changes with a single click.

### License
This project is licensed under the MIT License - see the LICENSE.md file for details.

### Changelog
|Major|Minor|Patch|Date|Notes|
|-|-|-|-|-|
|0|12|0|09/03/2023|License|
|0|11|0|09/03/2023|Readme|
|0|10|0|09/03/2023|Performance improvements|
|0|9|0|09/03/2023|CT004 Invalid CodeTag|
|0|8|0|09/03/2023|Tests for CodeFix for CT003|
|0|7|0|09/02/2023|CodeFix for CT003|
|0|6|0|09/02/2023|Tests for CT003|
|0|5|0|09/02/2023|CT003 Duplicate CodeTag|
|0|4|0|09/02/2023|Tests for CT002|
|0|3|0|09/01/2023|CT002 Unnecessary CodeTag|
|0|2|0|09/01/2023|Tests for CT001|
|0|1|0|09/01/2023|CT001 Missing CodeTag|

### Acknowledgments
For my team, The Specials, who put up with me

(in reverse alphabetical order)
* Saranya Theppala
* Mitch Thompson
* Mary Remo
* Leith Abudiab
* Harry Thomas Kibby IV
* Haroon Iqbal
* Carter Hill
* Becky Curnow
* Ashley Lee
