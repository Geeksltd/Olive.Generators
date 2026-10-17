using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Olive;

namespace OliveGenerator
{
    internal class DtoProgrammer
    {
        Type Type;

        static string[] CommonDefaultProperties = new[] { "title", "name", "subject", "description", "label", "text" };

        MemberInfo[] EffectiveProperties => Type.GetEffectiveProperties();

        public DtoProgrammer(Type type) => Type = type;

        /// <param name="nested">
        /// Pass true to declare a nested DTO inside a partial declaration of the classes that
        /// declare it, which is where the generated code looks for it. Without this the class was
        /// written at the top of the namespace while every reference to it still named the
        /// declaring class, so a controller with a nested DTO produced a package that never built.
        /// </param>
        internal string Generate(bool nested = false)
        {
            var r = new StringBuilder();

            r.AppendLine("namespace " + Type.Namespace);
            r.AppendLine("{");
            r.AppendLine("using System;");
            r.AppendLine("using System.Collections.Generic;");
            r.AppendLine("using System.Threading.Tasks;");
            r.AppendLine("using System.Reflection;");
            r.AppendLine("using Olive.Entities;");
            r.AppendLine();
            r.AppendLine(nested ? DtoTypes.WrapInDeclaringTypes(GenerateDto(), Type) : GenerateDto());

            r.AppendLine("}");

            return new CSharpFormatter(r.ToString()).Format();
        }

        string GenerateDto()
        {
            var r = new StringBuilder();

            r.AppendDocumentation(Type);

            r.AppendLine("[Olive.Entities.CacheObjects(false)]");
            r.AppendLine("public partial class " + Type.Name + " : Olive.Entities.GuidEntity");
            r.AppendLine("{");

            foreach (var p in EffectiveProperties)
            {
                var type = p.GetPropertyOrFieldType().GetProgrammingName(useGlobal: false, useNamespace: false, useNamespaceForParams: false, useCSharpAlias: true);

                if (!r.AppendDocumentation(p))
                    r.AppendLine($"/// <summary>Gets or sets the {p.Name} of this {Type.Name} instance.</summary>");

                r.AppendLine($"public {type} {p.Name} {{ get; set; }}");
                r.AppendLine();
            }

            r.AppendLine(GenerateTheToString());

            r.AppendLine("}");
            return r.ToString();
        }

        string GenerateTheToString() => $"public override string ToString() => {GetToStringField()};";

        string GetToStringField()
        {
            var explicitToStringField = EffectiveProperties.SingleOrDefault(i => i.GetAttribute("ToString") != null);
            var toStringField = explicitToStringField ?? SelectDefaultToStringField();

            if (toStringField != null) return toStringField.Name;

            // No prompt: this warning says itself that it changes nothing, and waiting for a key
            // that no build agent will ever press hangs the run instead of finishing it.
            Log.Warn($"Could not find a suitable implementation of ToString() for {Type.FullName}. " +
                "No field is annotated with [ToString], and none is named " +
                CommonDefaultProperties.Select(s => s.ToPascalCaseId()).ToString(", ", " or ") + ". " +
                "This does not stop the proxy creation.");

            return $"\"{Type.Name.RemoveBeforeAndIncluding("+")}: No good ToString() :(\"";
        }

        MemberInfo SelectDefaultToStringField()
        {
            MemberInfo result;

            // Priority 1 = Accurate:
            result = CommonDefaultProperties.Select(n => EffectiveProperties.FirstOrDefault(p => p.Name.ToLower() == n)).FirstOrDefault(p => p != null);
            if (result != null) return result;

            // Priority 2 = Contains:
            result = CommonDefaultProperties.Select(n => EffectiveProperties.FirstOrDefault(p => p.Name.ToLower().Contains(n))).FirstOrDefault(p => p != null);
            if (result != null) return result;

            return null;
        }
    }
}