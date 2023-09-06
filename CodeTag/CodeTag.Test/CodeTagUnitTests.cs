using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = CodeTag.Test.CSharpCodeFixVerifier<
    CodeTag.CodeTagAnalyzer,
    CodeTag.CodeTagCodeFixProvider>;

namespace CodeTag.Test
{
    [TestClass]
    public class CodeTagUnitTest
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task DefaultEmptyCase()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MissingCodeTag1()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        [DefineCodeTag]
        public C C { get; set; } = default!;

        public void {|#0:GoB|}()
        {
            C.GoC();
        }
    }

    public class C
    {
        public void GoC() { }
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoB", "Test.B.C").WithMessage("""Consider adding [CodeTag("Test.B.C")] to element 'GoB'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MissingCodeTag2()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        public void {|#0:GoA|}()
        {
            B.GoB();
        }
    }

    public class B
    {
        [DefineCodeTag]
        public C C { get; set; } = default!;

        [CodeTag(""Test.B.C"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        public void GoC() { }
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA", "Test.B.C").WithMessage("""Consider adding [CodeTag("Test.B.C")] to element 'GoA'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task NoDiagnostics1()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.B.C"")]
        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        [DefineCodeTag]
        public C C { get; set; } = default!;

        [CodeTag(""Test.B.C"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        public void GoC() { }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MissingCodeTag3()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        public void {|#0:GoB|}()
        {
            C.GoC();
        }
    }

    public class C
    {
        public int D { get; set; } = default;

        [DefineCodeTag]
        public void GoC() 
        { 
            D = 5;
        }
    }
}";


            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoB").WithMessage("""Consider adding [CodeTag("Test.C.GoC")] to element 'GoB'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task UnnecessaryCodeTag1()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class C
    {
        [CodeTag(""Test.C.GoC"")]
        public void {|#0:GoC|}() { }
    }
}";

            var expected = VerifyCS.Diagnostic("CT002").WithLocation(0).WithArguments("Test.C.GoC", "GoC").WithMessage("""Unnecessary CodeTag [CodeTag("Test.C.GoC")] on element 'GoC'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task NoDiagnostics2()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task DuplicateCodeTags2()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        [CodeTag(""Test.C.GoC"")]
        public void {|#0:GoB|}()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            var expected = VerifyCS.Diagnostic("CT003").WithLocation(0).WithArguments("Test.C.GoC", "GoB").WithMessage("""Duplicate CodeTag [CodeTag("Test.C.GoC)"] on element 'GoB'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task DuplicateCodeTags3()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        [CodeTag(""Test.C.GoC"")]
        public void {|#0:GoA|}()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            var expected = VerifyCS.Diagnostic("CT003").WithLocation(0).WithArguments("Test.C.GoC", "GoA").WithMessage("""Duplicate CodeTag [CodeTag("Test.C.GoC)"] on element 'GoA'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task CodeFixCT003_1()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        [CodeTag(""Test.C.GoC"")]
        public void {|#0:GoB|}()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            var fixedTest = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            var expected = VerifyCS.Diagnostic("CT003").WithLocation(0).WithArguments("Test.C.GoC", "GoB").WithMessage("""Duplicate CodeTag [CodeTag("Test.C.GoC)"] on element 'GoB'""");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixCT003_2()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        [CodeTag(""Test.C.GoC"")]
        public void {|#0:GoA|}()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            var fixedTest = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            var expected = VerifyCS.Diagnostic("CT003").WithLocation(0).WithArguments("Test.C.GoC", "GoA").WithMessage("""Duplicate CodeTag [CodeTag("Test.C.GoC)"] on element 'GoA'""");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixCT003_3()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        [DefineCodeTag(""Test.C.GoC"")]
        public void {|#0:GoA|}()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            var fixedTest = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoA()
        {
            B.GoB();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public void GoB()
        {
            C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public void GoC() { }
    }
}";

            var expected = VerifyCS.Diagnostic("CT003").WithLocation(0).WithArguments("Test.C.GoC", "GoA").WithMessage("""Duplicate CodeTag [CodeTag("Test.C.GoC)"] on element 'GoA'""");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task MissingCodeTagInLambda()
        {
            var test = @"
using System;
using System.Linq;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        public void {|#0:GoA|}()
        {
            var result = Enumerable.Range(0, 10).Select(x => B.GoB()).ToList();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;
        
        [CodeTag(""Test.C.GoC"")]
        public int GoB()
        {
            return C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public int GoC() 
        { 
            return 5;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA").WithMessage("""Consider adding [CodeTag("Test.C.GoC")] to element 'GoA'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MissingCodeTagInNestedLambda()
        {
            var test = @"
using System;
using System.Linq;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        public B B { get; set; } = default!;

        public void {|#0:GoA|}()
        {
            var groups = Enumerable.Range(0, 10).GroupBy(x => x % 2 == 0).Select(g => g.Select(x => B.GoB()).ToList()).ToList();
        }
    }

    public class B
    {
        public C C { get; set; } = default!;

        [CodeTag(""Test.C.GoC"")]
        public int GoB()
        {
            return C.GoC();
        }
    }

    public class C
    {
        [DefineCodeTag]
        public int GoC() 
        { 
            return 5;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA").WithMessage("""Consider adding [CodeTag("Test.C.GoC")] to element 'GoA'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task ConstructorReferencesTaggedProperty()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        [DefineCodeTag]
        public int B { get; set; } = 5;

        public {|#0:A|}()
        {
            var x = B;
        }
    }
}";

            // Expecting that the constructor should be flagged for missing the CodeTag.
            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments(".ctor").WithMessage("Consider adding [CodeTag(\"Test.A.B\")] to element '.ctor'");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodInvokesTaggedConstructor()
        {
            var test = @"
using System;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(""The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor."", nameof(key));
        }
    }

    public class A
    {
        [DefineCodeTag]
        public A()
        {
            // Tagged constructor
        }

        public void {|#0:B|}()
        {
            var instance = new A();
        }
    }
}";

            // Expecting that the MyMethod should be flagged for missing the CodeTag.
            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("Test.A..ctor", "B").WithMessage("""Consider adding [CodeTag("Test.A..ctor")] to element 'B'""");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

    }
}
