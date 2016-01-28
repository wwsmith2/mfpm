using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Reflection;

namespace EnumHelper
{
    public class EnumHelper
    {
        public static string GetEnumDescription(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attributes != null && attributes.Length > 0)
            {
                return attributes[0].Description;
            }
            else
            {
                return value.ToString();
            }
        }

        public static Enum GetEnumFromDescription(string description, Type enumType)
        {
            foreach (Enum value in Enum.GetValues(enumType))
            {
                if (GetEnumDescription(value).CompareTo(description) == 0)
                {
                    return value;
                }
            }
            throw new ArgumentException("Did not find " + description + " in " + enumType.ToString());
        }

        public static Enum GetEnumFromDescription<T>(string description)
        {
            Type enumType = typeof(T);
            // Can't use generic type constraints on value types,
            // so have to do check like this
            if (enumType.BaseType != typeof(Enum))
            {
                throw new ArgumentException("T must be of type System.Enum");
            }

            Enum retVal = null;
            Array enumValArray = Enum.GetValues(enumType);

            List<string> listDescriptions = EnumDescriptionList<T>();

            for (int i = 0; i < enumValArray.Length; i++)
            {
                if (listDescriptions[i] == description)
                {
                    retVal = (Enum)enumValArray.GetValue(i);
                    break;
                }
            }
            return retVal;
        }

        public static IEnumerable<T> EnumToList<T>()
        {
            Type enumType = typeof(T);
            // Can't use generic type constraints on value types,
            // so have to do check like this
            if (enumType.BaseType != typeof(Enum))
            {
                throw new ArgumentException("T must be of type System.Enum");
            }

            Array enumValArray = Enum.GetValues(enumType);
            List<T> enumValList = new List<T>(enumValArray.Length);

            foreach (int val in enumValArray)
            {
                enumValList.Add((T)Enum.Parse(enumType, val.ToString()));
            }
            return enumValList;
        }

        public static List<string> EnumDescriptionList<T>()
        {
            List<string> listDescriptions = new List<string>();

            Type enumType = typeof(T);
            // Can't use generic type constraints on value types,
            // so have to do check like this
            if (enumType.BaseType != typeof(Enum))
            {
                throw new ArgumentException("T must be of type System.Enum");
            }

            Array enumValArray = Enum.GetValues(enumType);

            foreach (int val in enumValArray)
            {
                Enum enumVal = (Enum)Enum.ToObject(typeof(T), val);   // (Enum)val;
                listDescriptions.Add(GetEnumDescription(enumVal));
            }
            return listDescriptions;
        }
    }
}
