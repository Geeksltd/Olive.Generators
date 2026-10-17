using Olive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OliveGenerator
{
    internal class HashHelper
    {
        /// <summary>
        /// Bump this whenever a release of this generator changes what it writes in a way that
        /// consumers need. Every command is republished once as a result, and only then: a release
        /// that leaves the generated packages as they were must not.
        /// </summary>
        const int OutputVersion = 1;

        /// <summary>
        /// Hashes everything which the command class is generated from: its members, its nested types
        /// and their members, and the documentation of each of them.
        /// <para>
        /// Only what ends up in the package counts. Updating any of the target application's packages
        /// used to republish every command, because the CI agent added every assembly reference of
        /// the application to the hash. What the generated project references now goes in here, by
        /// major version only, and nothing else about the application's dependencies does.
        /// </para>
        /// </summary>
        public static string HashCommand(Type command)
        {
            var members = command.GetCommandMembers();

            // The same nested types the class is generated from. A nested enum is hashed below,
            // with the other enums of the package, rather than twice and as a class here.
            var nestedTypes = command.GetCommandNestedTypes();

            if (members.None() && nestedTypes.None()) return string.Empty;

            var result = new StringBuilder();

            // The documentation is included so that a comment change also republishes the package.
            result.Append($"v{OutputVersion}-" + Context.Current.CommandName + "-" + XmlDocumentation.GetSummary(command) + "-");
            result.Append(EventBusCommandProjectCreator.Packages.Select(x => x.ForHash).ToString(",") + "-");
            result.Append(HashMembers(members));

            foreach (var nested in nestedTypes)
            {
                result.Append(nested.FullName + "-" + XmlDocumentation.GetSummary(nested) + "-");
                result.Append(HashMembers(nested.GetCommandMembers()));
            }

            // The package also contains a class for every type which the command refers to:
            foreach (var dto in DtoTypes.All)
            {
                result.Append(dto.FullName + "-" + XmlDocumentation.GetSummary(dto) + "-");
                result.Append(HashMembers(dto.GetEffectiveProperties()));
            }

            foreach (var type in DtoTypes.Enums) result.Append(HashEnum(type));

            return HashString(result.ToString());
        }

        /// <summary>
        /// Hashes an enum, the value of each member included, because the generated enum
        /// declares those values explicitly.
        /// </summary>
        static string HashEnum(Type type)
        {
            var result = new StringBuilder();

            result.Append($"{type.FullName}({Enum.GetUnderlyingType(type).Name}," +
                $"{type.IsDefined(typeof(FlagsAttribute), inherit: false)},{XmlDocumentation.GetSummary(type)})-");

            foreach (var name in Enum.GetNames(type))
                result.Append($"{name}={Enum.Format(type, Enum.Parse(type, name), "D")}" +
                    $"({XmlDocumentation.GetSummary(type.GetField(name))}),");

            return result.ToString();
        }

        static string HashMembers(IEnumerable<MemberInfo> members)
        {
            var result = new StringBuilder();

            foreach (var member in members)
            {
                var memberType = member.GetPropertyOrFieldType();

                result.Append(memberType?.FullName + "-");

                result.Append($"{member.Name}({memberType?.FullName},{GetAttributes(member)}," +
                    $"{member.GetAttribute("ToString") != null},{XmlDocumentation.GetSummary(member)}),");
            }

            return result.ToString();
        }

        static string GetAttributes(MemberInfo member) =>
            (member as FieldInfo)?.Attributes.ToString() ?? (member as PropertyInfo)?.Attributes.ToString();

        public static string HashString(string inputstr)
        {
            if (inputstr.IsEmpty()) return string.Empty;

            using (var sha = new System.Security.Cryptography.SHA512Managed())
            {
                var textBytes = Encoding.UTF8.GetBytes(inputstr);
                var hashBytes = sha.ComputeHash(textBytes);
                return BitConverter.ToString(hashBytes);
            }
        }
    }
}