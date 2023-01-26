using System;
using System.Linq;
using System.Reflection;
using Olive;

namespace OliveGenerator
{
    static class Extensions
    {
        public static MethodInfo FindDatabaseGetMethod(this Type type)
        {
            var providers = Context.Current.ActionMethods
                .Select(x => x.Method)
                .Select(x => new
                {
                    Method = x,
                    ProviderOf = x.GetDataProviderEntity()
                })
                .Where(x => x.ProviderOf != null);

            return providers
                .FirstOrDefault(x => x.ProviderOf.FullName == type.FullName)
                ?.Method;
        }

        public static Type GetApiMethodReturnType(this MethodInfo @this)
        {
            return @this.GetAttribute("Returns")?.ConstructorArguments.Single().Value as Type;
        }

        public static bool HasReturnType(this MethodGenerator @this) => !@this.ReturnType.IsEmpty();
    }
}