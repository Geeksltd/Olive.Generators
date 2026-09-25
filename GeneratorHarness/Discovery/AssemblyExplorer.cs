using System.Reflection;
using System.Runtime.InteropServices;
using Olive.GeneratorHarness.Tools;

namespace Olive.GeneratorHarness.Discovery;

/// <summary>
/// Lists what the generators would find in a target application's assembly.
/// <para>
/// This reads metadata only, through a MetadataLoadContext that is disposed as soon as the scan
/// finishes. Nothing in the target runs, and the files are not held open, so the microservice can
/// be rebuilt and rescanned without restarting the harness. The generators themselves do the
/// opposite: they load the assembly for real and instantiate its types. So this is an
/// approximation of their predicates, and the run remains the authority.
/// </para>
/// </summary>
public static class AssemblyExplorer
{
    /// <summary>The base class of a data endpoint, as DataEndPointGenerator casts to it.</summary>
    const string EndpointBase = "Olive.Entities.Replication.SourceEndpoint";

    /// <summary>The base class of an event bus command, the same constant the generator matches.</summary>
    const string CommandBase = "Olive.EventBusMessage";

    /// <summary>The attributes that expose a controller method, as MethodGenerator.IsApiAction reads them.</summary>
    static readonly string[] RoutingAttributes =
        ["Route", "HttpGet", "HttpPost", "HttpPut", "HttpPatch", "HttpDelete"];

    static readonly string[] HttpVerbAttributes =
        ["HttpGet", "HttpPost", "HttpPut", "HttpPatch", "HttpDelete"];

    public static DiscoveryResult Scan(string assemblyPath)
    {
        var result = new DiscoveryResult
        {
            AssemblyPath = assemblyPath,
            Built = SafeWriteTime(assemblyPath),
        };

        if (!File.Exists(assemblyPath))
        {
            result.Failure = "The assembly was not found: " + assemblyPath;
            return result;
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(assemblyPath)) ?? ".";

        MetadataLoadContext? context = null;

        try
        {
            context = new MetadataLoadContext(new PathAssemblyResolver(ProbingPaths(folder)));

            var assembly = context.LoadFromAssemblyPath(assemblyPath);

            return Populate(result, assembly);
        }
        catch (Exception ex)
        {
            result.Failure = ex.Message;
            return result;
        }
        finally
        {
            // Immediately, so the target's dlls are not held open until the next scan.
            context?.Dispose();
        }
    }

    static DiscoveryResult Populate(DiscoveryResult result, Assembly assembly)
    {
        var types = TypesOf(assembly, result);

        result.AssemblyFullName = assembly.FullName;
        result.TypeCount = types.Count(x => x != null);

        foreach (var type in types)
        {
            if (type == null) continue;

            // Per type, because a target application references assemblies that are not beside
            // it, and one whose base type lives in a missing one must not cost every other type
            // its classification. This is why the loop is not simply a set of Where clauses.
            try { Classify(result, type); }
            catch (Exception ex)
            {
                Note(result, ex);

                result.Unclassified.Add(Describe(ToolKind.DataEndpoint, type,
                    "could not be read: " + ex.Message.FirstLine()) with
                { Unresolved = true });
            }
        }

        Sort(result.Endpoints);
        Sort(result.Commands);
        Sort(result.Controllers);
        Sort(result.AllTypes);
        Sort(result.Unclassified);

        return result;
    }

    /// <summary>Puts one type into whichever list the generators would find it in.</summary>
    static void Classify(DiscoveryResult result, Type type)
    {
        if (IsVisible(type)) result.AllTypes.Add(Describe(ToolKind.DataEndpoint, type, ""));

        if (!type.IsClass) return;

        var chain = BaseChain(type, out var resolved);

        if (!type.IsAbstract && chain.Contains(EndpointBase)) result.Endpoints.Add(Endpoint(type));
        else if (!type.IsAbstract && chain.Contains(CommandBase)) result.Commands.Add(Command(type));
        else
        {
            var controller = Controller(type);

            if (controller != null) result.Controllers.Add(controller);
            else if (!resolved && !type.IsAbstract && IsVisible(type))
                result.Unclassified.Add(Describe(ToolKind.DataEndpoint, type,
                    "base type could not be resolved") with
                { Unresolved = true });
        }
    }

    static bool IsVisible(Type type)
    {
        try { return type.IsPublic || type.IsNestedPublic; }
        catch { return false; }
    }

    static void Sort(List<DiscoveredItem> items)
        => items.Sort((left, right) => string.CompareOrdinal(left.FullName, right.FullName));

    static Type?[] TypesOf(Assembly assembly, DiscoveryResult result)
    {
        Type?[] types;

        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;

            foreach (var loader in ex.LoaderExceptions)
                Note(result, loader);
        }

        return types;
    }

    static void Note(DiscoveryResult result, Exception? error)
    {
        if (error is FileNotFoundException missing && missing.FileName.HasContent())
            result.UnresolvedAssemblies.Add(new AssemblyName(missing.FileName).Name.Or(missing.FileName));
        else if (error is TypeLoadException load && load.TypeName.HasContent())
            result.UnresolvedAssemblies.Add(load.TypeName);
    }

    static DiscoveredItem Endpoint(Type type)
    {
        var warnings = new List<string>();

        // The generator does EndpointType.CreateInstance<SourceEndpoint>(), so a constructor
        // taking arguments would fail there rather than here.
        if (!HasParameterlessConstructor(type))
            warnings.Add("no parameterless constructor, so the generator cannot instantiate it");

        return Describe(ToolKind.DataEndpoint, type, "") with { Warnings = warnings };
    }

    static DiscoveredItem Command(Type type)
    {
        var nested = SafeNestedTypes(type).Count(x => !x.IsEnum);

        return Describe(ToolKind.EventBusCommand, type,
            nested == 0 ? "" : nested + (nested == 1 ? " nested type" : " nested types"));
    }

    /// <summary>
    /// A controller, or null when the type declares no api action. Mirrors the generator: methods
    /// declared on the type itself, public and instance, carrying a routing attribute and no
    /// NonAction.
    /// </summary>
    static DiscoveredItem? Controller(Type type)
    {
        MethodInfo[] methods;

        try { methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public); }
        catch { return null; }

        var actions = new List<MethodInfo>();
        var warnings = new List<string>();

        foreach (var method in methods)
        {
            if (Has(method, "NonAction")) continue;
            if (!RoutingAttributes.Any(x => Has(method, x))) continue;

            actions.Add(method);

            // Both of these make MethodGenerator throw once the run reaches this method.
            if (!HttpVerbAttributes.Any(x => Has(method, x)))
                warnings.Add(method.Name + "() declares a route but no http verb");
            else if (!Has(method, "HttpGet") && CountBodyParameters(method) > 1)
                warnings.Add(method.Name + "() is not a GET and takes more than one argument");
        }

        if (actions.Count == 0) return null;

        return Describe(ToolKind.ApiProxy, type,
            actions.Count + (actions.Count == 1 ? " api action" : " api actions")) with
        { Warnings = warnings };
    }

    static int CountBodyParameters(MethodInfo method)
    {
        try { return method.GetParameters().Length; }
        catch { return 0; }
    }

    /// <summary>
    /// Whether the member carries an attribute of that name. Read from the metadata rather than
    /// constructed: a MetadataLoadContext cannot instantiate an attribute.
    /// </summary>
    static bool Has(MemberInfo member, string name)
    {
        try
        {
            return member.GetCustomAttributesData().Any(x =>
                x.AttributeType.Name == name || x.AttributeType.Name == name + "Attribute");
        }
        catch { return false; }
    }

    static bool HasParameterlessConstructor(Type type)
    {
        try { return type.GetConstructor(Type.EmptyTypes) != null; }
        catch { return true; }
    }

    static Type[] SafeNestedTypes(Type type)
    {
        try { return type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic); }
        catch { return []; }
    }

    /// <summary>
    /// The full names of every base type, and whether the walk reached the root. A missing
    /// assembly stops it, which is why an incomplete target folder empties the lists.
    /// </summary>
    static HashSet<string> BaseChain(Type type, out bool resolved)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var current = type;
        resolved = true;

        for (var depth = 0; depth < 64; depth++)
        {
            Type? next;

            try { next = current.BaseType; }
            catch { resolved = false; return names; }

            if (next == null) return names;

            if (next.FullName.HasContent()) names.Add(next.FullName);

            current = next;
        }

        return names;
    }

    static DiscoveredItem Describe(ToolKind kind, Type type, string detail) => new()
    {
        Kind = kind,
        FullName = type.FullName.Or(type.Name),
        ShortName = type.Name,
        Namespace = type.Namespace,
        Detail = detail,
    };

    /// <summary>
    /// Everything the resolver may need: the target's own folder, every shared framework
    /// installed beside the one this harness runs on, and the harness's own folder.
    /// <para>
    /// The shared frameworks matter as much as the target folder. A framework dependent web
    /// application's bin folder holds neither System.Private.CoreLib nor any of
    /// Microsoft.AspNetCore.App, so without them the base type of every controller is
    /// unresolvable and the scan finds nothing at all.
    /// </para>
    /// </summary>
    static IEnumerable<string> ProbingPaths(string targetFolder)
    {
        var paths = new List<string>();

        foreach (var folder in Folders(targetFolder))
        {
            try
            {
                if (Directory.Exists(folder)) paths.AddRange(Directory.GetFiles(folder, "*.dll"));
            }
            catch { /* An unreadable probing folder is not worth failing the scan for. */ }
        }

        // The target's own copy of an assembly wins: PathAssemblyResolver keeps the first of a
        // simple name, and the version the microservice was built against is the right one.
        return paths.DistinctBy(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase);
    }

    static IEnumerable<string> Folders(string targetFolder)
    {
        yield return targetFolder;

        foreach (var shared in SharedFrameworks()) yield return shared;

        yield return AppContext.BaseDirectory;
    }

    /// <summary>
    /// The newest build of every shared framework installed on this machine, found from the one
    /// the harness itself is running on: .../shared/Microsoft.NETCore.App/&lt;version&gt;.
    /// </summary>
    static IEnumerable<string> SharedFrameworks()
    {
        string[] frameworks;

        try
        {
            var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());

            // .../shared/Microsoft.NETCore.App/10.0.11 -> .../shared
            var shared = runtime.Parent?.Parent;

            if (shared == null || !shared.Exists) return [runtime.FullName];

            frameworks = shared.GetDirectories()
                .Select(Newest)
                .Where(x => x != null)
                .Select(x => x!)
                .ToArray();
        }
        catch { return []; }

        return frameworks;
    }

    /// <summary>The highest numbered build of one shared framework.</summary>
    static string? Newest(DirectoryInfo framework)
    {
        try
        {
            return framework.GetDirectories()
                .Select(x => (Folder: x.FullName, Version: Parse(x.Name)))
                .Where(x => x.Version != null)
                .OrderByDescending(x => x.Version)
                .Select(x => x.Folder)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    static Version? Parse(string name)
    {
        // A preview build carries a suffix the Version parser will not take.
        var number = name.Split('-')[0];

        return Version.TryParse(number, out var version) ? version : null;
    }

    static DateTime? SafeWriteTime(string path)
    {
        try { return File.Exists(path) ? File.GetLastWriteTime(path) : null; }
        catch { return null; }
    }
}
