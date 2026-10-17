using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Olive;

namespace OliveGenerator
{
    class DtoTypes
    {
        public static List<Type> All { get; private set; }

        public static List<Type> Enums { get; private set; }

        public static List<Type> DomainEntities { get; private set; }

        static Assembly AssemblyObj;

        public static void FindAll(IEnumerable<Type> baseType, Assembly assemblyObj)
        {
            AssemblyObj = assemblyObj;

            All = baseType
                .ExceptNull()
                .Distinct()
                .Select(x => GetDefinableType(x))
                .ExceptNull()
                .Distinct()
                .ToList();

            DomainEntities = All.Where(x => IsEntity(x)).ToList();
            All = All.Except(DomainEntities).ToList();
            Enums = All.Where(x => x.IsEnum).ToList();
            All = All.Except(Enums).ToList();

            while (All.Any(t => Crawl(t))) continue;
        }

        static Type GetDefinableType(Type type)
        {
            if (type.IsArray) return GetDefinableType(type.GetElementType());

            if (type.IsA<IEnumerable>() && type.GenericTypeArguments.IsSingle())
                return GetDefinableType(type.GenericTypeArguments.Single());

            if (IsEntity(type)) return type;

            if (type.Assembly != AssemblyObj) return null;

            return type;
        }

        static bool IsEntity(Type type) => type.IsSubclassOf(typeof(Olive.Entities.Entity));

        static bool Crawl(Type type)
        {
            foreach (var member in type.GetEffectiveProperties())
            {
                var memberType = GetDefinableType(member.GetPropertyOrFieldType());
                if (memberType == null || All.Contains(memberType)) continue;

                if (memberType.IsEnum)
                {
                    if (Enums.Lacks(memberType)) Enums.Add(memberType);
                }
                else
                {
                    All.Add(memberType);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Generates the enums which the generated code refers to.
        /// </summary>
        /// <param name="useNamespace">
        /// Pass true when the generated code refers to the enums by their full name, as the event bus
        /// commands do. The Api proxy refers to them by their name alone, so there they are generated
        /// in the global namespace, where that name resolves from any namespace.
        /// </param>
        public static void GenerateEnums(DirectoryInfo destFolder, bool useNamespace = false)
        {
            if (DtoTypes.Enums.None()) return;

            using var scope = Log.Step($"Write {DtoTypes.Enums.Count} enum(s)", LogLevel.Detailed);

            foreach (var type in DtoTypes.Enums)
            {
                var body = new StringBuilder();

                body.AppendDocumentation(type);

                if (type.IsDefined(typeof(FlagsAttribute), inherit: false)) body.AppendLine("[System.Flags]");

                body.AppendLine("public enum " + type.Name.RemoveBeforeAndIncluding("+") + GetUnderlyingType(type));
                body.AppendLine("{");

                // The value of each member is explicit, so it always matches the source enum:
                foreach (var name in Enum.GetNames(type))
                {
                    body.AppendDocumentation(type.GetField(name));
                    body.AppendLine($"{name} = {Enum.Format(type, Enum.Parse(type, name), "D")},");
                }

                body.AppendLine("}\r\n");

                var code = WrapInDeclaringTypes(body.ToString(), type);

                // A nested enum always goes inside its namespace, because the generated code refers
                // to it through its declaring class, which is generated in that namespace too.
                var theNamespace = useNamespace || type.IsNested ? type.Namespace : null;

                destFolder.GetFile("Enums.cs").AppendLine(WrapInNamespace(code, theNamespace));
            }
        }

        /// <summary>
        /// Wraps a nested type in a partial declaration of each of its declaring classes, which is
        /// how the generated code refers to it. The declaring class merges with its own generated
        /// class when there is one.
        /// </summary>
        internal static string WrapInDeclaringTypes(string code, Type type)
        {
            for (var declaringType = type.DeclaringType; declaringType != null; declaringType = declaringType.DeclaringType)
                code = "public partial class " + declaringType.Name + "\r\n{\r\n" + code + "}\r\n";

            return code;
        }

        /// <summary>
        /// The name the generated code refers to a type by, which for a nested type keeps the
        /// declaring types in front of it. That is where <see cref="WrapInDeclaringTypes"/> puts the
        /// declaration, and it is what Olive's GetProgrammingName already produces at every call
        /// site that names a member's type.
        /// </summary>
        internal static string QualifiedName(Type type)
        {
            if (type is null) return null;

            if (type.IsArray) return QualifiedName(type.GetElementType()) + "[]";

            var result = type.Name;

            for (var declaring = type.DeclaringType; declaring != null; declaring = declaring.DeclaringType)
                result = declaring.Name + "." + result;

            return result;
        }

        static string WrapInNamespace(string code, string theNamespace)
        {
            if (theNamespace.IsEmpty()) return code;

            return "namespace " + theNamespace + "\r\n{\r\n" + code + "}\r\n";
        }

        /// <summary>
        /// Gets the base type declaration of the specified enum, or null when it's the default of int.
        /// The aliases are written explicitly because the generated file imports no namespace.
        /// </summary>
        static string GetUnderlyingType(Type type)
        {
            switch (Enum.GetUnderlyingType(type).Name)
            {
                case "Byte": return " : byte";
                case "SByte": return " : sbyte";
                case "Int16": return " : short";
                case "UInt16": return " : ushort";
                case "UInt32": return " : uint";
                case "Int64": return " : long";
                case "UInt64": return " : ulong";
                default: return null;
            }
        }

        /// <param name="nested">
        /// Pass true when the generated code refers to a nested DTO through its declaring class, as
        /// the Api proxy does: the proxy class it is nested in is generated too, so the declaration
        /// goes inside a partial declaration of that class. The event bus commands generate their
        /// own nested classes inline instead, so there they stay where they are.
        /// </param>
        public static void GenerateDtoClasses(DirectoryInfo destFolder, bool nested = false)
        {
            foreach (var type in DtoTypes.All)
            {
                using var scope = Log.Step("Write DTO class " + QualifiedName(type), LogLevel.Detailed);
                destFolder.GetFile(FileName(type, nested)).WriteAllText(new DtoProgrammer(type).Generate(nested));
            }
        }

        /// <summary>
        /// The file a DTO class goes in. A nested one takes its declaring types into the name as
        /// well, so that two nested types sharing a simple name cannot overwrite one another.
        /// </summary>
        internal static string FileName(Type type, bool nested)
            => (nested ? QualifiedName(type) : type.Name) + ".cs";
    }
}