using Olive;
using System;
using System.Linq;
using System.Text;

namespace OliveGenerator
{
    class EventBusCommandClassProgrammer
    {
        static Type Command => Context.CommandType;
        static string ClassName => Command.Name;

        public static string Generate()
        {
            var r = new StringBuilder();

            r.AppendLine("namespace " + Command.Namespace);
            r.AppendLine("{");
            r.AppendLine("using System;");
            r.AppendLine("using System.Threading.Tasks;");
            r.AppendLine("using Olive;");
            r.AppendLine();

            r.AppendLine($"public class {ClassName} : EventBusMessage");
            r.AppendLine("{");

            foreach (var item in Context.CommandType.GetEffectiveProperties())
            {
                var propertyTypeString = AddProperty(item.GetPropertyOrFieldType());
                r.AppendLine($"public {propertyTypeString} {item.Name} {{ get; set; }}");
            }

            r.AppendLine();

            r.AppendLine($"public Task Publish () => EventBus.Queue<{Command.Namespace}.{ClassName}>().Publish(this);");

            r.AppendLine("}");
            r.AppendLine("}");

            return new CSharpFormatter(r.ToString()).Format();
        }

        static string AddProperty(Type propertyType)
        {
            var type = propertyType;
            var extraType = "";
            if (type.IsArray)
            {
                type = type.GetElementType();
                extraType = "[]";
            }

            bool isNullable;
            if (isNullable = type.IsNullable())
            {
                type = type.GetGenericArguments().Single();
            }

            var method = type.Name;

            switch (method)
            {
                case "Boolean": method = "bool"; break;
                case "Int32": method = "int"; break;
                case "Int64": method = "decimal"; break;
                case "String": method = "string"; break;
                default: break;
            }

            return method + extraType;
        }
    }
}