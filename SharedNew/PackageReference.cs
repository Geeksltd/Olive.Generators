namespace OliveGenerator
{
    /// <summary>
    /// A nuget package a generated project references, together with the version to reference it at.
    /// <para>
    /// The version comes from the microservice being generated from, so a generated package lines
    /// up with its publisher. The fallback is this generator's own default, used only when the
    /// target does not use the package at all, or when the target's version turns out to be on none
    /// of the feeds the temporary project can reach.
    /// </para>
    /// </summary>
    public class PackageReference
    {
        public string Package { get; set; }
        public string Version { get; set; }

        /// <summary>This generator's own default, used if the target's version cannot be restored.</summary>
        public string FallbackVersion { get; set; }

        /// <summary>
        /// Whether Version came from the microservice being generated from.
        /// <para>
        /// Recorded when the version is read, rather than inferred by comparing it against the
        /// fallback. The fallbacks here are the latest versions at the time of writing, so a target
        /// sitting on exactly one of them — the ordinary case for a service that is up to date — was
        /// reported as using this generator's default, and could not qualify for the rebuild that
        /// falls back to those defaults.
        /// </para>
        /// </summary>
        public bool IsFromTarget { get; set; }

        /// <summary>
        /// Whether rebuilding against this generator's own defaults would reference anything
        /// different. False when the target happens to use the fallback version already, which is
        /// ordinary for a service that is up to date, and in that case a rebuild is a second full
        /// build of the same thing that fails the same way.
        /// </summary>
        public bool CanFallBack => Version != FallbackVersion;

        /// <summary>
        /// How this reference goes into an item's hash: the package and its major version only. A
        /// minor or patch update of the publisher's packages leaves the published package usable, so
        /// it must not republish every item; a move to a new major version does.
        /// </summary>
        public string ForHash => Package + "@" + Version?.Split('.')[0];

        public static PackageReference MSharp => For("MSharp", fallback: "5.4.1");
        public static PackageReference Olive => For(nameof(Olive), fallback: "10.2.1");
        public static PackageReference Olive_Entities => For(nameof(Olive_Entities), fallback: "10.2.2");
        public static PackageReference Olive_Entities_Data => For(nameof(Olive_Entities_Data), fallback: "10.4.2");
        public static PackageReference Olive_EventBus => For(nameof(Olive_EventBus), fallback: "10.2.0");
        public static PackageReference Olive_ApiClient => For(nameof(Olive_ApiClient), fallback: "10.2.0");
        public static PackageReference Olive_Microservices => For(nameof(Olive_Microservices), fallback: "10.2.0");
        public static PackageReference Olive_Entities_Data_Replication => For(nameof(Olive_Entities_Data_Replication), fallback: "10.2.0");
        public static PackageReference Olive_Entities_Data_Replication_QueueUrlProvider => For(nameof(Olive_Entities_Data_Replication_QueueUrlProvider), fallback: "10.2.0");

        /// <summary>
        /// Takes the version from the microservice being generated from, so the generated package
        /// lines up with its publisher. The fallback applies only when the target does not use the
        /// package, and is the latest version at the time of writing.
        /// </summary>
        static PackageReference For(string package, string fallback)
        {
            var name = ToPackageName(package);
            var target = TargetPackageVersions.For(name);

            return new PackageReference
            {
                Package = name,
                Version = target ?? fallback,
                FallbackVersion = fallback,
                IsFromTarget = !string.IsNullOrWhiteSpace(target)
            };
        }

        static string ToPackageName(string packageName) => packageName.Replace("_", ".");
    }
}
