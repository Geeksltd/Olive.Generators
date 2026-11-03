using Olive;
using Olive.Entities;
using Olive.Entities.Replication;
using System;
using System.Linq;
using System.Text;

namespace OliveGenerator
{
    /// <summary>
    /// Generates <c>{Publisher}Service.Definitions.I{Entity}</c> interfaces for CareServe / non-M# consumers (AMP #182377).
    /// </summary>
    internal class SubscriptionInterfaceProgrammer
    {
        readonly ExposedType ExposedType;
        readonly string DefinitionsNamespace;

        Type EntityType => ExposedType.GetType();

        public SubscriptionInterfaceProgrammer(ExposedType exposedType, string publisherNamespace)
        {
            ExposedType = exposedType;
            DefinitionsNamespace = publisherNamespace + ".Definitions";
        }

        internal string Generate()
        {
            var interfaceName = "I" + EntityType.Name;

            var r = new StringBuilder();
            r.AppendLine("using System;");
            r.AppendLine("using Olive.Entities;");
            r.AppendLine();
            r.AppendLine("namespace " + DefinitionsNamespace);
            r.AppendLine("{");

            r.AppendDocumentation(ExposedType.DomainType);

            r.AppendLine("public interface " + interfaceName + " : IEntity");
            r.AppendLine("{");

            foreach (var field in ExposedType.Fields)
                r.AppendLine(AddProperty(field));

            r.AppendLine("}");
            r.AppendLine("}");

            return new CSharpFormatter(r.ToString()).Format();
        }

        string AddProperty(ExposedField field)
        {
            var name = field.GetName();
            var type = field.GetPropertyType();
            if (type.IsArray) type = type.GetElementType();

            var memberType = ToMemberType(type);
            var line = memberType + " " + name + " { get; }";

            var property = (field as ExposedPropertyInfo)?.Property;
            if (property == null) return line;

            var block = new StringBuilder();
            if (block.AppendDocumentation(property))
                return block.ToString().TrimEnd() + Environment.NewLine + line;

            return line;
        }

        /// <summary>
        /// Writes a type as C# that compiles in the subscription project, which references nothing of
        /// the publisher. An entity is written as the definition interface of the exposed type which
        /// replicates it, and as <c>IEntity</c> when this endpoint doesn't expose it. A generic type is
        /// written with its arguments, never as its reflection name, which C# can't parse.
        /// </summary>
        string ToMemberType(Type type)
        {
            if (type == null) return "object";

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) return ToMemberType(underlying) + "?";

            if (type.IsArray) return ToMemberType(type.GetElementType()) + "[]";

            if (typeof(IEntity).IsAssignableFrom(type) && !type.IsInterface)
                return Context.Current.ExposedTypes.FirstOrDefault(x => x.DomainType == type) is ExposedType exposed
                    ? "I" + exposed.GetType().Name
                    : nameof(IEntity);

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition().FullName;
                definition = definition.Remove(definition.IndexOf('`')).Replace('+', '.');
                return definition + "<" + type.GetGenericArguments().Select(ToMemberType).ToString(", ") + ">";
            }

            return PrimitiveOrTypeName(type);
        }

        static string PrimitiveOrTypeName(Type type)
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
    }
}
