using System.Collections.Generic;
using System.IO;
using System.Linq;
using Olive;

namespace System
{
    static class Extensions
    {
        internal static DirectoryInfo SolutionDirectory(this DirectoryInfo child)
        {
            var folders = new List<string>();

            for (var result = child; result != null; result = result.Parent)
            {
                folders.Add(result.FullName);
                if (result.GetFiles("*.sln").Any()) return result;
            }

            throw new Exception($"Could not find the solution folder. Looked in [{folders.ToString(",")}]");
        }
    }
}