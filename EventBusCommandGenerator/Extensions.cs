using Olive;
using System;
using System.Linq;
using System.Reflection;

namespace OliveGenerator
{
    static class Extensions
    {
        public static CustomAttributeData GetAttribute(this MemberInfo type, string attributeName)
        {
            return type.GetCustomAttributesData()
               .FirstOrDefault(x => x.AttributeType.Name == attributeName + "Attribute");
        }

        public static MemberInfo[] GetEffectiveProperties(this Type type)
        {
            return type.GetPropertiesAndFields(BindingFlags.Public | BindingFlags.Instance)
                 .Except(x => x.Name == "ID" && x.GetPropertyOrFieldType() == typeof(Guid))
                 .Except(x => x.Name == "DeduplicationId")
                 .ToArray();
        }
    }
}