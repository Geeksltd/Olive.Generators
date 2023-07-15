using Olive;
using System;
using System.Collections.Generic;
using System.Text;

namespace OliveGenerator
{
    internal class HashHelper
    {
        public static string HashExposedType(IEnumerable<dynamic> exposedType)
        {
            if (exposedType.HasAny() == false) return string.Empty;
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
            if (inputstr.IsEmpty()) return string.Empty;

            using (var sha = new System.Security.Cryptography.SHA512Managed())
            {
                var textBytes = Encoding.UTF8.GetBytes(inputstr);
                var hashBytes = sha.ComputeHash(textBytes);
                return BitConverter.ToString(hashBytes);
            }
        }
    }
}