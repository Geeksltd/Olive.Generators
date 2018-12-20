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

            foreach (var item in Context.CommandFields)
                r.AppendLine($"public {item.FieldType.Name} {item.Name} {{ get; set; }}");
            r.AppendLine();

            r.AppendLine($"public Task Publish () => EventBus.Queue<{Command.Namespace}.{ClassName}>().Publish(this);");

            r.AppendLine("}");
            r.AppendLine("}");

            return new CSharpFormatter(r.ToString()).Format();
        }


    }
}