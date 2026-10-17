using Olive;
using Olive.Entities;
using Olive.Entities.Replication;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OliveGenerator
{
    internal class HashHelper
    {
        /// <summary>
        /// Bump this whenever a release of this generator changes what it writes in a way that
        /// consumers need. Every endpoint is republished once as a result, and only then: a release
        /// that leaves the generated packages as they were must not.
        /// </summary>
        const int OutputVersion = 1;

        /// <summary>
        /// Hashes everything which the endpoint and its M# models are generated from, so that any
        /// change to them, the documentation included, republishes the package.
        /// <para>
        /// Only what ends up in the package counts. Updating any of the target application's packages
        /// used to republish every endpoint, because the CI agent added every assembly reference of
        /// the application to the hash. What the generated projects reference now goes in here, by
        /// major version only, and nothing else about the application's dependencies does.
        /// </para>
        /// </summary>
        public static string HashExposedType(IEnumerable<ExposedType> exposedType, IEnumerable<FileInfo> referenceDataFiles)
        {
            if (exposedType.HasAny() == false) return string.Empty;
            string exposedTypeToStr = $"v{OutputVersion}-" + Context.Current.EndpointName + "-";

            foreach (var type in exposedType)
            {
                var domainType = type?.DomainType;

                exposedTypeToStr += type?.GetType().FullName + "-" + domainType?.FullName + "-" +
                    XmlDocumentation.GetSummary(domainType) + "-";

                exposedTypeToStr += $"({type.IsSoftDeleteEnabled},{type.ToStringExpression}," +
                    $"{domainType?.GetProperty("ID")?.PropertyType?.FullName})-";

                foreach (var item in type.Fields)
                {
                    exposedTypeToStr += $"{item.GetName()}({item.GetPropertyType()?.FullName}," +
                        $"{item.GetTitle()},{item.IsAssociation},{GetPropertyDetails(item as ExposedPropertyInfo)}),";
                }
            }

            exposedTypeToStr += "-" + BuildInputs() + "-";
            exposedTypeToStr += HashReferenceData(referenceDataFiles);

            return HashString(exposedTypeToStr);
        }

        /// <summary>
        /// What the generated projects are built against: the major version of every package they
        /// reference, and the framework the M# package targets.
        /// </summary>
        static string BuildInputs()
        {
            var packages = EndpointProjectCreator.Packages.Concat(MSharpProjectCreator.Packages)
                .Select(x => x.ForHash).ToString(",");

            string framework;

            // Read from the M# installation, which a hash-only run otherwise never looks at. When it
            // cannot be read the generation fails anyway, and saying so is that run's job, not this one's.
            try { framework = MSharpProjectCreator.ModelFramework; }
            catch { framework = null; }

            return framework.Or("(no m# framework)") + "," + packages;
        }

        /// <summary>
        /// The reference data files are embedded in the endpoint package, so their content counts as
        /// well as their names: an edited file used to leave the hash, and so the package, unchanged.
        /// Line endings are normalised, because they depend on how the repository was checked out.
        /// </summary>
        static string HashReferenceData(IEnumerable<FileInfo> files)
        {
            return files.OrEmpty()
                .Select(x => new { Name = $"{x.Directory.Name}/{x.Name}", File = x })
                .OrderBy(x => x.Name)
                .Select(x => x.Name + ":" + HashString(File.ReadAllText(x.File.FullName).Replace("\r\n", "\n")))
                .ToString("|");
        }

        /// <summary>
        /// Gets the parts of an exposed property which affect the generated M# model: its
        /// documentation, its maximum length, and the association which it's the inverse of.
        /// </summary>
        static string GetPropertyDetails(ExposedPropertyInfo exposedProperty)
        {
            var property = exposedProperty?.Property;
            if (property == null) return null;

            return $"{XmlDocumentation.GetSummary(property)}," +
                $"{property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength}," +
                $"{exposedProperty.IsInverseAssociation}," +
                property.GetCustomAttribute<InverseOfAttribute>()?.Association;
        }

        public static string HashString(string inputStr)
        {
            if (inputStr.IsEmpty()) return string.Empty;

            using (var sha = new System.Security.Cryptography.SHA512Managed())
            {
                var textBytes = Encoding.UTF8.GetBytes(inputStr);
                var hashBytes = sha.ComputeHash(textBytes);
                return BitConverter.ToString(hashBytes);
            }
        }
    }
}