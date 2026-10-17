using Olive;
using System;
using System.Reflection;
using System.Text;

namespace OliveGenerator
{
    class EventBusCommandClassProgrammer
    {
        static Type Command => Context.Current.CommandType;
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

            r.AppendDocumentation(Command);

            // Partial, because a nested enum is generated into Enums.cs as a member of a partial
            // declaration of this same class, which is how the generated code refers to it.
            r.AppendLine($"public partial class {ClassName} : EventBusMessage");
            r.AppendLine("{");

            foreach (var item in Context.Current.CommandType.GetCommandMembers())
            {
                var propertyTypeString = item.GetPropertyOrFieldType().GetProgrammingName();
                r.AppendDocumentation(item);
                r.AppendLine($"public {propertyTypeString} {GetPropertyOrFieldName(item)} {{ get; set; }}");
            }

            foreach (var type in Context.Current.CommandType.GetCommandNestedTypes())
            {
                r.AppendDocumentation(type);
                r.AppendLine($"public class {type.Name}");
                r.AppendLine("{");
                foreach (var member in type.GetCommandMembers())
                {
                    var propertyTypeString = member.GetPropertyOrFieldType().GetProgrammingName();
                    r.AppendDocumentation(member);
                    r.AppendLine($"public {propertyTypeString} {GetPropertyOrFieldName(member)} {{ get; set; }}");
                }

                r.AppendLine("}");
            }

            r.AppendLine();

            r.AppendLine($"public Task Publish () => EventBus.Queue<{Command.Namespace}.{ClassName}>().Publish(this);");
            r.AppendLine();
            r.AppendLine($"public async Task<string> PublishAndBlockUntilProcessed()");
            r.AppendLine($"{{");
            r.AppendLine($"await Publish();");
            r.AppendLine(
                $"return await EventBus.ProcessCommandUrl<{Command.Namespace}.{ClassName}>().AsUri().Download();");
            r.AppendLine($"}}");

            r.AppendLine("}");
            r.AppendLine("}");

            return new CSharpFormatter(r.ToString()).Format();
        }

        static string GetPropertyOrFieldName(MemberInfo member)
        {
            if (DtoTypes.DomainEntities.Contains(member.GetPropertyOrFieldType())) return $"{member.Name}Id";
            return member.Name;
        }

        //static string AddProperty(Type propertyType)
        //{
        //    var type = propertyType;
        //    var extraType = "";

        //    if (type.IsArray)
        //    {
        //        type = type.GetElementType();
        //        extraType = "[]";
        //    }

        //    bool isNullable;

        //    if (isNullable = type.IsNullable())
        //    {
        //        type = type.GetGenericArguments().Single();
        //    }

        //    var method = type.Name;

        //    switch (method)
        //    {
        //        case "Boolean": method = "bool"; break;
        //        case "Int32": method = "int"; break;
        //        case "Int64": method = "decimal"; break;
        //        case "String": method = "string"; break;
        //        default: break;
        //    }

        //    if (DtoTypes.DomainEntities.Contains(type)) return "Guid?";

        //    return method + extraType;
        //}
    }
}