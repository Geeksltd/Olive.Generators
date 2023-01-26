using System;
using System.Linq;
using System.Reflection;
using Olive;

namespace OliveGenerator
{
    static class Extensions
    {
        // public static MemberInfo[] GetEffectiveProperties(this Type type)
        // {
        //    return type.GetPropertiesAndFields(BindingFlags.Public | BindingFlags.Instance)
        //         .Except(x => x.Name == "ID" && x.GetPropertyOrFieldType() == typeof(Guid))
        //         .Except(x => x.Name == "DeduplicationId")
        //         .ToArray();
        // }

        public static Type[] GetFiledTypes(this Type @this)
        {
            return @this.GetPropertiesAndFields(BindingFlags.Public | BindingFlags.Instance)
                 .ExceptNull()
                .Except(x => x.Name == "DeduplicationId")
                 .Select(x => x.GetPropertyOrFieldType())
                 .ToArray();
        }
    }
}