using System;

namespace WebUtility
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false)]
    public class InjectAttribute : Attribute
    {
        public string Key { get; set; }
        
        public InjectAttribute(string key = null)
        {
            Key = key;
        }
    }
}

