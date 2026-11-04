using Olive;
using Olive.Entities;
using Olive.Entities.Replication;
using System;
using System.Linq;
using System.Text;

namespace OliveGenerator
{
    /// <summary>
    /// Generates concrete <see cref="Entity{T}"/> types in the publisher namespace for EF replication imports.
    /// </summary>
    internal class SubscriptionEntityProgrammer
    {
        readonly ExposedType ExposedType;
        readonly string PublisherNamespace;

        Type EntityType => ExposedType.GetType();
        Type DomainType => ExposedType.DomainType;

        public SubscriptionEntityProgrammer(ExposedType exposedType, string publisherNamespace)
        {
            ExposedType = exposedType;
            PublisherNamespace = publisherNamespace;
        }

        internal string Generate()
        {
            var idType = GetIdTypeName();
            var r = new StringBuilder();

            r.AppendLine("using System;");
            r.AppendLine("using Olive.Entities;");
            r.AppendLine();
            r.AppendLine("namespace " + PublisherNamespace);
            r.AppendLine("{");

            r.AppendDocumentation(DomainType);
            r.AppendLine($"[Schema(\"{PublisherNamespace}\")]");
            r.AppendLine($"public class {EntityType.Name} : Entity<{idType}>");
            r.AppendLine("{");

            foreach (var field in ExposedType.Fields)
                AppendProperty(r, field);

            r.AppendLine("}");
            r.AppendLine("}");

            return new CSharpFormatter(r.ToString()).Format();
        }

        void AppendProperty(StringBuilder r, ExposedField field)
        {
            if (field.IsAssociation)
            {
                var navName = field.GetName();
                var target = Context.Current.ExposedTypes.FirstOrDefault(x =>
                    x.DomainType == field.GetPropertyType() || x.GetType().Name == navName);
                if (target != null)
                {
                    var idType = GetIdTypeName(target.DomainType);
                    r.AppendLine($"public {idType}? {navName}Id {{ get; set; }}");
                    r.AppendLine($"public {target.GetType().Name} {navName} {{ get; set; }}");
                    return;
                }
            }

            var memberType = SubscriptionTypeMapper.ToMemberType(field.GetPropertyType(), ExposedType);
            r.AppendLine($"public {memberType} {field.GetName()} {{ get; set; }}");
        }

        string GetIdTypeName() => GetIdTypeName(DomainType);

        static string GetIdTypeName(Type domainType)
        {
            var id = domainType?.GetProperty("ID")?.PropertyType ?? typeof(Guid);
            return SubscriptionTypeMapper.ToMemberType(id, null);
        }
    }
}
