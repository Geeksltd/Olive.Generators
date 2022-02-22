using Olive.Entities.Replication;
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Olive.SharedHelper
{
    internal class HashHelper
    {

        public static string HashExposedType(IEnumerable<ExposedType> exposedType)
        {
            if (exposedType.HasAny() == false) return String.Empty;
            string exposedTypeToStr = null;
            foreach (var type in exposedType)
            {
                exposedTypeToStr += type?.DomainType?.FullName + "-";
                foreach (var item in type.Fields)
                {
                    exposedTypeToStr += $"{item.GetName()}({item.GetPropertyType()?.FullName},{item.GetTitle()}),";
                }
            }
            return HashString(exposedTypeToStr);
        }

        public static string HashString(string inputstr)
        {
            if (inputstr.IsEmpty())
            {
                return string.Empty;
            }

            using (var sha = new System.Security.Cryptography.SHA512Managed())
            {
                var textBytes = Encoding.UTF8.GetBytes(inputstr);
                var hashBytes = sha.ComputeHash(textBytes);
                return BitConverter.ToString(hashBytes);
            }
        }
    }
}
