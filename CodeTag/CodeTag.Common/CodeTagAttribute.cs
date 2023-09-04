using System;

namespace CodeTag
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = true)]
    public sealed class CodeTagAttribute : Attribute
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public CodeTagAttribute(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        { }
    }
}
