namespace OliveGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Xml.Linq;
    using Olive;

    /// <summary>
    /// Provides the original documentation (/// comments) of the source types and members.
    /// It's read from the documentation file which the compiler generates beside their assembly,
    /// so that it can be carried into the generated code and therefore the generated nuget package.
    /// </summary>
    static class XmlDocumentation
    {
        static readonly Dictionary<string, Dictionary<string, XElement>> Documents =
            new Dictionary<string, Dictionary<string, XElement>>(StringComparer.OrdinalIgnoreCase);

        // The M# projects are generated in parallel, so the documents are loaded under a lock.
        static readonly object DocumentsLock = new object();

        /// <summary>
        /// Appends the documentation of the specified source member as /// comment lines.
        /// It returns false if the member has no documentation, in which case the caller
        /// should generate its own comment.
        /// </summary>
        public static bool AppendDocumentation(this StringBuilder builder, MemberInfo member)
        {
            var comments = GetComments(member);
            if (comments.IsEmpty()) return false;

            builder.AppendLine(comments);
            return true;
        }

        /// <summary>
        /// Gets the documentation of the specified source member as /// comment lines, or null.
        /// </summary>
        public static string GetComments(MemberInfo member) => ToComments(Find(member));

        /// <summary>
        /// Gets the plain text of the summary tag of the specified source member, or null.
        /// </summary>
        public static string GetSummary(MemberInfo member)
        {
            var summary = Find(member)?.Element("summary");
            if (summary == null) return null;

            var result = summary.Value.Split('\n').Select(x => x.Trim()).Where(x => x.HasValue()).ToString(" ");

            return result.HasValue() ? result : null;
        }

        /// <summary>
        /// Gets the summary of the specified source member as a C# string literal, or null.
        /// It's used where the documentation is passed to an Api rather than emitted as a comment,
        /// such as the Documentation() method of M# entity types and properties.
        /// </summary>
        public static string GetSummaryLiteral(MemberInfo member)
        {
            var summary = GetSummary(member);
            if (summary.IsEmpty()) return null;

            return "\"" + summary
                .Replace("\\", @"\\")
                .Replace("\"", @"\""")
                .Replace("\t", @"\t")
                .Replace("\r", @"\r")
                .Replace("\n", @"\n") + "\"";
        }

        static string ToComments(XElement member)
        {
            if (member == null) return null;

            var lines = member.Elements()
                .SelectMany(x => x.ToString().Split('\n'))
                .Select(x => x.Trim())
                .Where(x => x.HasValue())
                .Select(x => "/// " + x)
                .ToArray();

            if (lines.None()) return null;

            return lines.ToLinesString();
        }

        static XElement Find(MemberInfo member)
        {
            if (member == null) return null;

            var document = GetDocument(member as Type ?? member.DeclaringType);
            if (document == null) return null;

            var key = GetKey(member);
            if (key.IsEmpty()) return null;

            if (document.TryGetValue(key, out var result)) return result;

            if (member is MethodBase)
            {
                // The signature of an overload isn't always predictable. Fall back to the name:
                var prefix = key.RemoveFrom("(");

                var matches = document.Where(x => x.Key == prefix || x.Key.StartsWith(prefix + "(")).ToArray();
                if (matches.IsSingle()) return matches[0].Value;
            }

            return null;
        }

        static string GetKey(MemberInfo member)
        {
            if (member is Type type) return "T:" + GetFullName(type);

            var prefix = member switch
            {
                PropertyInfo _ => "P:",
                FieldInfo _ => "F:",
                MethodBase _ => "M:",
                EventInfo _ => "E:",
                _ => null
            };

            if (prefix == null) return null;

            var result = prefix + GetFullName(member.DeclaringType) + "." + member.Name.Replace('.', '#');

            if (member is MethodBase method) result += GetParameters(method);

            return result;
        }

        static string GetParameters(MethodBase method)
        {
            var parameters = method.GetParameters();
            if (parameters.None()) return null;

            return "(" + parameters.Select(x => GetParameterType(x.ParameterType)).ToString(",") + ")";
        }

        static string GetParameterType(Type type)
        {
            if (type.IsByRef) return GetParameterType(type.GetElementType()) + "@";

            if (type.IsArray)
                return GetParameterType(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";

            if (type.IsGenericParameter)
                return (type.DeclaringMethod == null ? "`" : "``") + type.GenericParameterPosition;

            if (type.IsConstructedGenericType)
                return GetFullName(type.GetGenericTypeDefinition()).RemoveFrom("`") + "{" +
                    type.GetGenericArguments().Select(GetParameterType).ToString(",") + "}";

            return GetFullName(type);
        }

        static string GetFullName(Type type)
        {
            if (type == null) return null;

            // The documentation of a generic type is defined against its generic definition:
            if (type.IsConstructedGenericType) type = type.GetGenericTypeDefinition();

            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        static Dictionary<string, XElement> GetDocument(Type type)
        {
            var assembly = type?.Assembly;
            var location = assembly?.Location;

            if (location.IsEmpty()) return null;

            lock (DocumentsLock)
            {
                if (Documents.TryGetValue(location, out var result)) return result;

                return Documents[location] = Load(Path.ChangeExtension(location, ".xml").AsFile(), assembly);
            }
        }

        static Dictionary<string, XElement> Load(FileInfo file, Assembly assembly)
        {
            if (!file.Exists())
            {
                Warn($"No documentation file was found at {file.FullName}. The /// comments of " +
                    $"{assembly.GetName().Name} will not be included in the generated package. To include them, " +
                    "add <GenerateDocumentationFile>true</GenerateDocumentationFile> to the source project.");

                return null;
            }

            try
            {
                return XDocument.Load(file.FullName)
                    .Root?.Element("members")?.Elements("member")
                    .Where(x => x.Attribute("name")?.Value.HasValue() == true)
                    .GroupBy(x => x.Attribute("name").Value)
                    .ToDictionary(x => x.Key, x => x.First());
            }
            catch (Exception ex)
            {
                Warn($"Failed to read the documentation file {file.FullName}: {ex.Message}");
                return null;
            }
        }

        static void Warn(string message) => Log.Warn(message);
    }
}
