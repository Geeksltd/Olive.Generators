using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Olive;

namespace OliveGenerator
{
    public class MethodGenerator
    {
        /// <summary>The only verb without a request body, so its arguments go in the query string.</summary>
        const string Get = "Get";

        static readonly string[] HttpVerbs = { Get, "Post", "Put", "Patch", "Delete" };

        /// <summary>The attributes that expose a controller method, most specific route first.</summary>
        static readonly string[] RoutingAttributes =
            new[] { "Route" }.Concat(HttpVerbs.Select(x => "Http" + x)).ToArray();

        internal MethodInfo Method;
        string[] RouteParams;

        /// <summary>
        /// The type the proxy method returns, named as the generated code names it. A nested
        /// return type keeps its declaring class, which is where its DTO class is declared.
        /// </summary>
        public string ReturnType => DtoTypes.QualifiedName(Method.GetApiMethodReturnType());
        Type ControllerType;

        /// <summary>
        /// Whether the method is exposed as an api action. A controller may declare public methods
        /// that are not actions, and a proxy method generated for one of those would name no http
        /// verb and so would not compile.
        /// </summary>
        public static bool IsApiAction(MethodInfo method)
        {
            if (method.GetAttribute("NonAction") != null) return false;

            return RoutingAttributes.Any(x => method.GetAttribute(x) != null);
        }

        public MethodGenerator(MethodInfo method, Type controllerType)
        {
            Method = method;
            ControllerType = controllerType;
            RouteParams = new RouteTemplate(Route()).Parameters.Select(x => x.Key).ToArray();
        }

        public string Generate(string PublisherService)
        {
            var r = new StringBuilder();

            var behaviour = $"Sends a Http{HttpVerb()} request to {Route()} of the {PublisherService} service.";

            if (Method.GetExplicitAuthorizeServiceAttribute().HasValue())
                behaviour += $" As the target Api declares [{Method.GetExplicitAuthorizeServiceAttribute()}], I will call AsServiceUser() automatically.";

            // Keep the documentation of the Api method itself, and describe the proxy behaviour separately:
            if (r.AppendDocumentation(Method)) r.AppendLine($"/// <remarks>{behaviour}</remarks>");
            else r.AppendLine($"/// <summary>{behaviour}</summary>");

            r.AppendLine($"public Task{ReturnType.WithWrappers("<", ">")} {Method.Name}({GetArgs()})");
            r.AppendLine("{");
            // Inject the mock data here
            r.AppendLine("if(MockConfig.Enabled)");
            r.AppendLine("{");
            r.AppendLine($"return MockConfig.Expect.{Method.Name}Result({GetArguments().Keys.ToString(", ")});");
            r.AppendLine("}");

            if (Method.GetExplicitAuthorizeServiceAttribute().HasValue())
                r.AppendLine("this.AsServiceUser();");

            if (RouteParams.Any())
            {
                r.AppendLine($"var routeTemplate = new RouteTemplate(\"{Route()}\");");
                r.AppendLine($"var url = routeTemplate.Merge({RouteParams.ToString(", ").WithWrappers("new { ", "}")});");
            }
            else r.AppendLine($"var url = \"{Route()}\";");

            r.AppendLine();
            r.AppendLine($"var client = Microservice.Of(\"{PublisherService}\").Api(url);");
            r.AppendLine("foreach (var config in Configurators) config(client);");
            r.AppendLine();
            r.AppendLine(GetReturnStatement());
            r.AppendLine("}");

            return r.ToString();
        }

        string GetReturnStatement()
        {
            var verb = HttpVerb();

            var r = new StringBuilder();
            r.Append($"return client.{verb}");
            if (verb == Get) r.Append($"<{ReturnType.Or("object")}>");
            r.AppendLine("(" + GetArg() + ");");
            return r.ToString();
        }

        public string GetArg()
        {
            var nonRoute = Method.GetParameters().Where(x => RouteParams.Lacks(x.Name)).ToArray();
            var parameters = nonRoute.Select(x => x.Name).Trim().ToArray();

            if (parameters.None()) return null;

            if (HttpVerb() == Get)
            {
                // A single object is passed as it is: the api client turns its properties into the
                // query string, which is what a [FromQuery] model binds from. Wrapping it would send
                // "?request=Namespace.RequestDto" instead, and the published proxies always passed it bare.
                if (nonRoute.IsSingle() && !IsScalar(nonRoute.Single().ParameterType)) return parameters.Single();

                // A GET has no request body, so its other arguments travel together as query parameters.
                return "new { " + parameters.ToString(", ") + "}";
            }

            if (parameters.HasMany())
                throw new Exception($"{ControllerType.Name}.{Method.Name}() takes {parameters.Length} arguments " +
                    $"that are not route values, and an Http{HttpVerb()} api method can send only one as its body.");

            return parameters.Single();
        }

        /// <summary>A value that binds from a single query parameter, rather than an object whose properties do.</summary>
        static bool IsScalar(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
                type == typeof(Guid);
        }

        public string GetArgs() => GetArguments().Select(x => x.Value + " " + x.Key).ToString(", ");

        public string GetMockKeyExpression()
        {
            return GetArguments().Select(x => x.Value + ":{" + x.Key + "}").ToString(" | ");
        }

        /// <summary>
        /// Keys are arg names. Values are their types.
        /// </summary>
        Dictionary<string, string> GetArguments()
        {
            return Method.GetParameters().ToDictionary(x => x.Name,
                  x => x.ParameterType.GetProgrammingName(useGlobal: false, useNamespace: false, useNamespaceForParams: false, useCSharpAlias: true));
        }

        public string GetArgsNames()
        {
            var items = Method.GetParameters().Select(x => x.Name).ToList();
            return string.Join(", ", items);
        }

        public string GetArgsTypes() => GetArguments().Values.ToString(", ");

        string HttpVerb()
        {
            var result = HttpVerbs.FirstOrDefault(x => Method.GetAttribute("Http" + x) != null);

            if (result.IsEmpty())
                throw new Exception($"{ControllerType.Name}.{Method.Name}() declares a route but no http verb, " +
                    "so the method to call cannot be determined. Add [HttpGet], [HttpPost], [HttpPut], " +
                    "[HttpPatch] or [HttpDelete] to it, or [NonAction] if it is not an api method.");

            return result;
        }

        string Route()
        {
            var classRoute = ControllerType
                      .GetAttribute("Route")
                      ?.ConstructorArguments
                      .FirstOrDefault().Value.ToStringOrEmpty();

            var methodRoute = GetMethodRoute();

            if (methodRoute.IsEmpty()) return classRoute.OrEmpty();

            return classRoute.WithSuffix("/") + methodRoute;
        }

        string GetMethodRoute()
        {
            foreach (var name in RoutingAttributes)
            {
                var result = Method.GetAttribute(name)
                             ?.ConstructorArguments
                             .FirstOrDefault().Value.ToStringOrEmpty();

                if (result.HasValue()) return result;
            }

            // A verb attribute with no template of its own, e.g. [HttpGet], routes to the
            // controller's own route.
            return string.Empty;
        }

        public Type[] GetArgAndReturnTypes()
        {
            return Method.GetParameters().Select(x => x.ParameterType)
                .Concat(Method.GetApiMethodReturnType()).ToArray();
        }
    }
}