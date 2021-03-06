using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Piral.Blazor.Core
{
    static class Extensions
    {
        private static readonly IDictionary<Type, string[]> allowedArgs = new Dictionary<Type, string[]>();

        private class Match
        {
            public IDictionary<string, object> @params { get; set; }
        }

        public static IDictionary<string, object> AdjustArguments(this Type type, IDictionary<string, object> args)
        {
            if (!allowedArgs.TryGetValue(type, out var allowed))
            {
                allowed = type.GetProperties()
                    .Where(m => m.GetCustomAttributes(typeof(ParameterAttribute), true).Any())
                    .Select(m => m.Name)
                    .ToArray();

                allowedArgs.Add(type, allowed);
            }

            var allArgs = new List<IDictionary<string, object>> { args };
            try
            {
                var routeParams = JsonSerializer.Deserialize<Match>(JsonSerializer.Serialize(args["match"]))?.@params;

                if (!routeParams.IsNullOrEmpty())
                {
                    allArgs.Add(routeParams);
                }
            }
            catch (KeyNotFoundException) { }

            var adjustedArgs = allArgs
                .SelectMany(dict => dict)
                .Where(m => allowed.Contains(m.Key))
                .ToDictionary(m => m.Key, m => type.NormalizeValue(m.Key, m.Value));

            return adjustedArgs;
        }

        public static object NormalizeValue(this Type type, string key, object value)
        {
            var property = type.GetProperty(key);
            var propType = property.PropertyType;

            if (value is null || (value is JsonElement e && e.ValueKind == JsonValueKind.Null))
            {
                return propType.GetDefaultValue();
            }

            if (value.GetType() == propType)
            {
                return value;
            }

            var converter = TypeDescriptor.GetConverter(propType);
            return converter.ConvertFrom(value.ToString());
        }

        public static object GetDefaultValue(this Type t)
        {
            if (t.IsValueType)
            {
                return Activator.CreateInstance(t);
            }
            else
            {
                return null;
            }
        }

        public static bool IsNullOrEmpty<TKey, TValue>(this IDictionary<TKey, TValue> collection)
        {
            return (collection == null || collection.Count < 1);
        }

        public static IEnumerable<Type> GetTypesWithAttributes(this Assembly assembly,
            IReadOnlyCollection<Type> attributeTypes)
        {
            return assembly?.GetTypes().Where(m => m.HasAnyAttribute(attributeTypes)) ?? Enumerable.Empty<Type>();
        }

        public static bool HasAnyAttribute(this Type member, IEnumerable<Type> attributeTypes)
        {
            return attributeTypes.Any(attributeType => Attribute.IsDefined(member, attributeType));
        }
    }
}
