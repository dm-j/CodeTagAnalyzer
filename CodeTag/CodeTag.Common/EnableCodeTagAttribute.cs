using System;

namespace CodeTag
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class EnableCodeTagAttribute : Attribute
    {
        public EnableCodeTagAttribute() { }
    }
}
