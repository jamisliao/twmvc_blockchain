using System.ComponentModel;
using System.Reflection;

namespace twmvc_example.Extensions;

public static class EnumExtensions
{
    /// <summary>
    ///     GetEnumDescription
    /// </summary>
    /// <typeparam name="TEnum">Enum</typeparam>
    /// <param name="type">type</param>
    /// <returns>string</returns>
    public static string GetEnumDescription<TEnum>(this TEnum type)
    {
        var fi = type.GetType().GetField(type.ToString());
        if (fi!.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute attributes)
        {
            return attributes.Description;
        }

        return type.ToString();
    }
}