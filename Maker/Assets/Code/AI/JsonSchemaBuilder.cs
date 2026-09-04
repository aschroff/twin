using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace Code.AI
{
    /// <summary>
    /// Builds the JSON schema OpenAI structured outputs need, from a C# type.
    ///
    /// Unlike a flat generator this walks nested classes and lists, so a response can be a list
    /// of objects. Strict mode is what OpenAI validates against, and it demands three things of
    /// every object: all properties listed in "required", "additionalProperties": false, and an
    /// "items" schema for every array. Anything missing is rejected before the model even runs.
    ///
    /// Members are read the way Newtonsoft reads them - public properties and public fields, with
    /// [JsonProperty] naming them when it is present.
    ///
    /// A value list can be injected for a member that has to come from data rather than from the
    /// type, keyed by its path ("paintings.regionKeys" for the body regions, which only exist at
    /// runtime). For an array member the list applies to its items.
    /// </summary>
    public static class JsonSchemaBuilder
    {
        /// <summary>Guard against a type that refers to itself.</summary>
        public const int MaxDepth = 10;

        /// <summary>The whole "text.format" value of a request: the schema plus its envelope.</summary>
        public static object Format(Type type, IDictionary<string, IEnumerable<string>> allowedValues = null)
        {
            return new
            {
                type = "json_schema",
                name = type.Name,
                schema = Schema(type, allowedValues),
                strict = true
            };
        }

        /// <summary>The schema of <paramref name="type"/> itself.</summary>
        public static object Schema(Type type, IDictionary<string, IEnumerable<string>> allowedValues = null)
        {
            return Of(type, "", allowedValues ?? new Dictionary<string, IEnumerable<string>>(), 0);
        }

        private static object Of(Type type, string path, IDictionary<string, IEnumerable<string>> allowed, int depth)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            IEnumerable<string> values;
            if (allowed.TryGetValue(path, out values) && !IsList(type))
            {
                return new { type = "string", @enum = new List<string>(values) };
            }

            if (type == typeof(string)) return new { type = "string" };
            if (type == typeof(bool)) return new { type = "boolean" };
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
                return new { type = "integer" };
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return new { type = "number" };

            if (type.IsEnum)
            {
                return new { type = "string", @enum = new List<string>(Enum.GetNames(type)) };
            }

            Type element = ElementType(type);
            if (element != null)
            {
                // an injected value list on an array member describes what the items may be
                if (allowed.TryGetValue(path, out values))
                {
                    return new { type = "array", items = new { type = "string", @enum = new List<string>(values) } };
                }
                // the items keep the path of the list, so a member inside them is addressed as
                // "paintings.regionKeys" rather than "paintings[].regionKeys"
                return new { type = "array", items = Of(element, path, allowed, depth + 1) };
            }

            if (depth >= MaxDepth)
            {
                throw new InvalidOperationException(
                    $"JsonSchemaBuilder: '{path}' is nested deeper than {MaxDepth} - does the type refer to itself?");
            }

            var properties = new Dictionary<string, object>();
            var required = new List<string>();
            foreach (var member in Members(type))
            {
                string name = NameOf(member);
                string memberPath = path.Length == 0 ? name : path + "." + name;
                properties[name] = Of(TypeOf(member), memberPath, allowed, depth + 1);
                // strict mode has no optional members: everything is required
                required.Add(name);
            }

            if (properties.Count == 0)
            {
                throw new InvalidOperationException(
                    $"JsonSchemaBuilder: '{(path.Length == 0 ? type.Name : path)}' has no serializable members.");
            }

            return new
            {
                type = "object",
                properties,
                required = required.ToArray(),
                additionalProperties = false
            };
        }

        /// <summary>The item type of a list or an array, null when this is not a list.</summary>
        private static Type ElementType(Type type)
        {
            if (type == typeof(string)) return null;
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>) || definition == typeof(IList<>)
                    || definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }
            return null;
        }

        private static bool IsList(Type type)
        {
            return ElementType(type) != null;
        }

        private static List<MemberInfo> Members(Type type)
        {
            var members = new List<MemberInfo>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                if (property.GetGetMethod() == null) continue;
                if (IsIgnored(property)) continue;
                members.Add(property);
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsIgnored(field)) continue;
                members.Add(field);
            }
            return members;
        }

        private static bool IsIgnored(MemberInfo member)
        {
            return member.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length > 0;
        }

        private static string NameOf(MemberInfo member)
        {
            var attributes = member.GetCustomAttributes(typeof(JsonPropertyAttribute), false);
            if (attributes.Length > 0)
            {
                string name = ((JsonPropertyAttribute)attributes[0]).PropertyName;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return member.Name;
        }

        private static Type TypeOf(MemberInfo member)
        {
            var property = member as PropertyInfo;
            return property != null ? property.PropertyType : ((FieldInfo)member).FieldType;
        }
    }
}
