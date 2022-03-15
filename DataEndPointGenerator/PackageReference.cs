using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class PackageReference
{
    public string Package { get; set; }
    public string Version { get; set; }

    public static PackageReference MSharp => new PackageReference { Package = "Msharp", Version = "5.2.201" };
    public static PackageReference Olive => new PackageReference { Package = nameof(Olive), Version = "2.1.204" };
    public static PackageReference Olive_Entities => new PackageReference { Package = ToPackageName(nameof(Olive_Entities)), Version = "2.1.161" };
    public static PackageReference Olive_Entities_Data_Replication => new PackageReference { Package = ToPackageName(nameof(Olive_Entities_Data_Replication)), Version = "2.1.150" };
    public static PackageReference Olive_Entities_Data_Replication_QueueUrlProvider => new PackageReference { Package = ToPackageName(nameof(Olive_Entities_Data_Replication_QueueUrlProvider)), Version = "1.0.0" };
    private static string ToPackageName(string packageName) => packageName.Replace("_", ".");

}
