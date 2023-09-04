using System;

namespace CodeTag
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public sealed class DefineCodeTagAttribute : Attribute
    {
        public DefineCodeTagAttribute() { }

        public DefineCodeTagAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("The tag cannot be null, empty, or whitespace. To autogenerate a tag, use the parameterless constructor.", nameof(key));
        }
    }
}