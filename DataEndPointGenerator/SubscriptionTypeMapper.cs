using Olive.Entities;
using Olive.Entities.Replication;
using System;
using System.Linq;

namespace OliveGenerator
{
    internal static class SubscriptionTypeMapper
    {
        public static string ToMemberType(Type type, ExposedType currentEndpointType)
        {
            if (type == null) return "object";

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) return ToMemberType(underlying, currentEndpointType) + "?";

            if (type.IsArray) return ToMemberType(type.GetElementType(), currentEndpointType) + "[]";

            if (typeof(IEntity).IsAssignableFrom(type) && !type.IsInterface)
            {
                var exposed = Context.Current.ExposedTypes.FirstOrDefault(x => x.DomainType == type)
                    ?? Context.Current.ExposedTypes.FirstOrDefault(x => x.GetType().Name == type.Name);
                if (exposed != null) return exposed.GetType().Name;
                return nameof(IEntity);
            }

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition().FullName;
                definition = definition.Remove(definition.IndexOf('`')).Replace('+', '.');
                return definition + "<" + string.Join(", ", type.GetGenericArguments().Select(t => ToMemberType(t, currentEndpointType))) + ">";
            }

            return PrimitiveOrTypeName(type);
        }

        public static string PrimitiveOrTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(short)) return "short";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(char)) return "char";
            if (type == typeof(object)) return "object";
            if (type == typeof(Guid)) return "Guid";
            if (type == typeof(DateTime)) return "DateTime";
            if (type == typeof(DateTimeOffset)) return "DateTimeOffset";
            if (type == typeof(TimeSpan)) return "TimeSpan";
            return type.FullName?.Replace('+', '.') ?? type.Name;
        }

        public static string ToInterfaceMemberType(Type type, ExposedType currentEndpointType)
        {
            if (type == null) return "object";

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) return ToInterfaceMemberType(underlying, currentEndpointType) + "?";

            if (typeof(IEntity).IsAssignableFrom(type) && !type.IsInterface)
            {
                var exposed = Context.Current.ExposedTypes.FirstOrDefault(x => x.DomainType == type);
                return exposed != null ? "I" + exposed.GetType().Name : nameof(IEntity);
            }

            return ToMemberType(type, currentEndpointType);
        }
    }
}
