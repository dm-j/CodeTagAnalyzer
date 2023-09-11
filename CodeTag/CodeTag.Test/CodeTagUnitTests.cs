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
        private const string Start = @"
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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class EnableCodeTagAttribute : Attribute
    {
        public EnableCodeTagAttribute() { }
    }

    [EnableCodeTag]
    public class Wrapper
    {
";

        //No diagnostics expected to show up
        [TestMethod]
        public async Task DefaultEmptyCase()
        {
            var test = Start + @"}}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MissingCodeTag1()
        {
            var test = Start + @"
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoB", "Test.Wrapper.B.C").WithMessage("Element 'GoB': Missing Code Tag Test.Wrapper.B.C");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MissingCodeTag2()
        {
            var test = Start + @"
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

            [CodeTag(""Test.Wrapper.B.C"")]
            public void GoB()
            {
                C.GoC();
            }
        }

        public class C
        {
            public void GoC() { }
        }
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA", "Missing Code Tag", "Test.Wrapper.B.C").WithMessage("Element 'GoA': Missing Code Tag Test.Wrapper.B.C");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task NoDiagnostics1()
        {
            var test = Start + @"

        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.B.C"")]
            public void GoA()
            {
                B.GoB();
            }
        }

        public class B
        {
            [DefineCodeTag]
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.B.C"")]
            public void GoB()
            {
                C.GoC();
            }
        }

        public class C
        {
            public void GoC() { }
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MissingCodeTag3()
        {
            var test = Start + @"

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
    }
}";


            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoB", "Missing Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoB': Missing Code Tag Test.Wrapper.C.GoC");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task UnnecessaryCodeTag1()
        {
            var test = Start + @"
        public class C
        {
            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void {|#0:GoC|}() { }
        }
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoC", "Unnecessary Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoC': Unnecessary Code Tag Test.Wrapper.C.GoC");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task NoDiagnostics2()
        {
            var test = Start + @"

        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void GoA()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task DuplicateCodeTags2()
        {
            var test = Start + @"

        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void GoA()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoB", "Duplicate Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoB': Duplicate Code Tag Test.Wrapper.C.GoC");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task DuplicateCodeTags3()
        {
            var test = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void {|#0:GoA|}()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA", "Duplicate Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoA': Duplicate Code Tag Test.Wrapper.C.GoC");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task CodeFixCT003_1()
        {
            var test = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void GoA()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var fixedTest = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void GoA()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoB", "Duplicate Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoB': Duplicate Code Tag Test.Wrapper.C.GoC");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixCT003_2()
        {
            var test = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void {|#0:GoA|}()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var fixedTest = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void GoA()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA", "Duplicate Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoA': Duplicate Code Tag Test.Wrapper.C.GoC");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixCT003_UnnecessaryCodeTag()
        {
            var test = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            [CodeTag(""MissingTag"")]
            public void {|#0:GoA|}()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var fixedTest = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void GoA()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA", "Duplicate Code Tag", "MissingTag").WithMessage("Element 'GoA': Duplicate Code Tag Test.Wrapper.C.GoC");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixCT003_MissingCodeTag()
        {
            var test = Start + @"
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
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var fixedTest = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
            public void GoA()
            {
                B.GoB();
            }
        }

        public class B
        {
            public C C { get; set; } = default!;

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA", "Missing Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoA': Missing Code Tag Test.Wrapper.C.GoC");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task MissingCodeTagInLambda()
        {
            var test = Start + @"
        public class A
        {
            public B B { get; set; } = default!;

            public void {|#0:GoA|}()
            {
                var result = Enumerable.Range(0, 10).Select(x => B.GoB());
            }
        }

        public class B
        {
            public C C { get; set; } = default!;
            
            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA", "Missing Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoA': Missing Code Tag Test.Wrapper.C.GoC");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MissingCodeTagInNestedLambda()
        {
            var test = Start + @"
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

            [CodeTag(""Test.Wrapper.C.GoC"")]
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("GoA", "Missing Code Tag", "Test.Wrapper.C.GoC").WithMessage("Element 'GoA': Missing Code Tag Test.Wrapper.C.GoC");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task ConstructorReferencesTaggedProperty()
        {
            var test = Start + @"
        public class A
        {
            [DefineCodeTag]
            public int B { get; set; } = 5;

            public {|#0:A|}()
            {
                var x = B;
            }
        }
    }
}";

            // Expecting that the constructor should be flagged for missing the CodeTag.
            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments(".ctor", "Missing Code Tag", "Test.Wrapper.A.B").WithMessage("Element '.ctor': Missing Code Tag Test.Wrapper.A.B");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodInvokesTaggedConstructor()
        {
            var test = Start + @"
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
    }
}";

            var expected = VerifyCS.Diagnostic("CT001").WithLocation(0).WithArguments("B", "Missing Code Tag", "Test.Wrapper.A..ctor").WithMessage("Element 'B': Missing Code Tag Test.Wrapper.A..ctor");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

    }
}
