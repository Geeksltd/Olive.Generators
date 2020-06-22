using Olive;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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


            DomainEntities = All.Where(x=>IsEntity(x)).ToList();
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

        public static void GenerateEnums(DirectoryInfo destFolder)
        {
            if (DtoTypes.Enums.None()) return;

            Console.Write("Adding Enums ...");

            foreach (var type in DtoTypes.Enums)
            {
                var file = destFolder.GetFile("Enums.cs");
                file.AppendLine("public enum " + type.Name.RemoveBeforeAndIncluding("+"));
                file.AppendLine("{");
                file.AppendLine(Enum.GetNames(type).ToString(",\r\n"));
                file.AppendLine("}\r\n");
            }
        }

        public static void GenerateDtoClasses(DirectoryInfo destFolder)
        {
            foreach (var type in DtoTypes.All)
            {
                Console.Write("Adding DTO class " + type.Name + "...");
                var dto = new DtoProgrammer(type);
                destFolder.GetFile(type.Name + ".cs").WriteAllText(dto.Generate());
                Console.WriteLine("Done");
            }
        }
    }
}