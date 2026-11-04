using Olive;
using Olive.Entities;
using Olive.Entities.Replication;
using System;
using System.Linq;
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

            var memberType = SubscriptionTypeMapper.ToInterfaceMemberType(type, ExposedType);
            var line = memberType + " " + name + " { get; }";

            var property = (field as ExposedPropertyInfo)?.Property;
            if (property == null) return line;

            var block = new StringBuilder();
            if (block.AppendDocumentation(property))
                return block.ToString().TrimEnd() + Environment.NewLine + line;

            return line;
        }
    }
}
