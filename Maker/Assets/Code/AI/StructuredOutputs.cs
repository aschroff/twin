using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Code.AI.StructuredOutputs
{
    /// <summary>
    /// Base class for structured output models.
    /// Similar to Pydantic BaseModel in Python.
    /// </summary>
    [Serializable]
    public abstract class StructuredOutputBase
    {
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static T FromJson<T>(string json) where T : StructuredOutputBase
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }

    /// <summary>
    /// Example: Medical finding structured output
    /// Customize this based on your actual use case
    /// </summary>
    [Serializable]
    public class MedicalFinding : StructuredOutputBase
    {
        [JsonProperty("part_name")]
        public string PartName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("severity")]
        public string Severity { get; set; }

        [JsonProperty("treatment_recommendation")]
        public string TreatmentRecommendation { get; set; }

        [JsonProperty("confidence")]
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Example: List of medical findings
    /// </summary>
    [Serializable]
    public class MedicalFindingsList : StructuredOutputBase
    {
        [JsonProperty("findings")]
        public List<MedicalFinding> Findings { get; set; } = new List<MedicalFinding>();

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("overall_severity")]
        public string OverallSeverity { get; set; }
    }

    /// <summary>
    /// Example: Simple text response wrapper
    /// Use when you want structured output but just need text
    /// </summary>
    [Serializable]
    public class TextResponse : StructuredOutputBase
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    /// <summary>
    /// Example: Part description with metadata
    /// </summary>
    [Serializable]
    public class PartDescription : StructuredOutputBase
    {
        [JsonProperty("part_name")]
        public string PartName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("key_observations")]
        public List<string> KeyObservations { get; set; } = new List<string>();

        [JsonProperty("suggested_actions")]
        public List<string> SuggestedActions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Helper class to generate JSON schemas for structured output types.
    /// This is a simple implementation - you may want to use a more robust schema generator.
    /// </summary>
    public static class SchemaGenerator
    {
        /// <summary>
        /// Generate a basic JSON schema for a type.
        /// For production use, consider using NJsonSchema or similar library.
        /// </summary>
        public static object GenerateSchema<T>() where T : StructuredOutputBase
        {
            var type = typeof(T);
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var prop in type.GetProperties())
            {
                var jsonProp = prop.GetCustomAttributes(typeof(JsonPropertyAttribute), false);
                var propName = jsonProp.Length > 0
                    ? ((JsonPropertyAttribute)jsonProp[0]).PropertyName
                    : prop.Name;

                // Determine type
                var propType = prop.PropertyType;
                string schemaType;

                if (propType == typeof(string))
                {
                    schemaType = "string";
                }
                else if (propType == typeof(int) || propType == typeof(long))
                {
                    schemaType = "integer";
                }
                else if (propType == typeof(float) || propType == typeof(double))
                {
                    schemaType = "number";
                }
                else if (propType == typeof(bool))
                {
                    schemaType = "boolean";
                }
                else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    schemaType = "array";
                }
                else
                {
                    schemaType = "object";
                }

                properties[propName] = new { type = schemaType };
            }

            return new
            {
                type = "object",
                properties,
                required = required.ToArray()
            };
        }
    }
}
