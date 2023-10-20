using Olive;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace OliveGenerator
{
    internal class HashHelper
    {
        public static string HashFieldTypes(IEnumerable<FieldInfo> fieldTypes)
        {
            if (fieldTypes.HasAny() == false) return string.Empty;
            string fieldTypeToStr = Context.Current.CommandName + "-";

            foreach (var type in fieldTypes)
            {
                fieldTypeToStr += type?.FieldType?.FullName + "-";

                fieldTypeToStr += $"{type.Name}({type.GetPropertyOrFieldType()?.FullName},{type.Attributes}),";
            }

            return HashString(fieldTypeToStr);
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