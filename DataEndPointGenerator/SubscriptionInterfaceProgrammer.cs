using Olive;
using Olive.Entities.Replication;
using System;
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

        static string ToMemberType(Type type)
        {
            if (type == null) return "object";

            var nullable = false;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                nullable = true;
                type = type.GetGenericArguments()[0];
            }

            var name = PrimitiveOrTypeName(type);
            return nullable ? name + "?" : name;
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
            if (type.IsEnum) return type.FullName.Replace('+', '.');
            if (type.IsArray) return PrimitiveOrTypeName(type.GetElementType()) + "[]";

            return type.FullName?.Replace('+', '.') ?? type.Name;
        }
    }
}
