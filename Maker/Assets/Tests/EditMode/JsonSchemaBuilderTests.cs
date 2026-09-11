using System.Collections.Generic;
using Code;
using Code.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace EditModeTests
{
    /// <summary>
    /// The JSON schema the OpenAI structured outputs are requested with. Strict mode is
    /// unforgiving: every object needs all of its properties in "required" plus
    /// "additionalProperties": false, and every array needs "items". A schema that misses one of
    /// those is rejected before the model runs, so the invariants are checked recursively here
    /// rather than one property at a time.
    ///
    /// EditMode on purpose: these need no scene, and a PlayMode test that does not derive from
    /// PlayModeTestBase would start the real app against the real data path.
    /// </summary>
    [Category(Processes.Technical)]
    public class JsonSchemaBuilderTests
    {
        private class Leaf
        {
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("count")] public int Count { get; set; }
            [JsonProperty("weight")] public float Weight { get; set; }
            [JsonProperty("done")] public bool Done { get; set; }
        }

        private class Branch
        {
            [JsonProperty("title")] public string Title { get; set; }
            [JsonProperty("leaves")] public List<Leaf> Leaves { get; set; }
            [JsonProperty("tags")] public List<string> Tags { get; set; }
            [JsonProperty("single")] public Leaf Single { get; set; }
        }

        private class SelfReferencing
        {
            [JsonProperty("child")] public SelfReferencing Child { get; set; }
        }

        private static JObject Schema<T>(IDictionary<string, IEnumerable<string>> allowed = null)
        {
            return JObject.Parse(JsonConvert.SerializeObject(JsonSchemaBuilder.Schema(typeof(T), allowed)));
        }

        [Test]
        public void Schema_OfNestedTypes_DescribesEveryLevel()
        {
            JObject schema = Schema<Branch>();

            Assert.AreEqual("object", schema["type"].ToString());
            Assert.AreEqual("string", schema["properties"]["title"]["type"].ToString());

            // a list of objects: an array with a full object schema as its items
            JToken leaves = schema["properties"]["leaves"];
            Assert.AreEqual("array", leaves["type"].ToString());
            Assert.AreEqual("object", leaves["items"]["type"].ToString());
            Assert.AreEqual("string", leaves["items"]["properties"]["name"]["type"].ToString());
            Assert.AreEqual("integer", leaves["items"]["properties"]["count"]["type"].ToString());
            Assert.AreEqual("number", leaves["items"]["properties"]["weight"]["type"].ToString());
            Assert.AreEqual("boolean", leaves["items"]["properties"]["done"]["type"].ToString());

            // a list of strings
            Assert.AreEqual("array", schema["properties"]["tags"]["type"].ToString());
            Assert.AreEqual("string", schema["properties"]["tags"]["items"]["type"].ToString());

            // a nested object that is not a list
            Assert.AreEqual("object", schema["properties"]["single"]["type"].ToString());
            Assert.AreEqual("string", schema["properties"]["single"]["properties"]["name"]["type"].ToString());

            AssertStrict(schema, "Branch");
        }

        [Test]
        public void Schema_WithAllowedValues_LimitsTheMemberToThoseValues()
        {
            var allowed = new Dictionary<string, IEnumerable<string>>
            {
                { "tags", new[] { "left", "right" } },
                { "single.name", new[] { "one", "two", "three" } }
            };
            JObject schema = Schema<Branch>(allowed);

            // on a list member the values describe the items
            JToken tags = schema["properties"]["tags"];
            Assert.AreEqual("array", tags["type"].ToString());
            Assert.AreEqual("string", tags["items"]["type"].ToString());
            CollectionAssert.AreEqual(new[] { "left", "right" }, Strings(tags["items"]["enum"]));

            // on a plain member they describe the member
            JToken name = schema["properties"]["single"]["properties"]["name"];
            Assert.AreEqual("string", name["type"].ToString());
            CollectionAssert.AreEqual(new[] { "one", "two", "three" }, Strings(name["enum"]));

            // a member without an entry stays open
            Assert.IsNull(schema["properties"]["title"]["enum"]);
        }

        [Test]
        public void Schema_OfTheDocumentMapping_IsStrictAndCarriesTheRegionKeys()
        {
            var regions = new[] { "chest_left", "chest_right", "shin_left" };
            var allowed = new Dictionary<string, IEnumerable<string>>
            {
                { "paintings.regionKeys", regions }
            };
            JObject schema = Schema<DocumentMapping>(allowed);

            AssertStrict(schema, "DocumentMapping");

            JToken paintings = schema["properties"]["paintings"];
            Assert.AreEqual("array", paintings["type"].ToString());
            JToken keys = paintings["items"]["properties"]["regionKeys"];
            Assert.AreEqual("array", keys["type"].ToString());
            CollectionAssert.AreEqual(regions, Strings(keys["items"]["enum"]));

            // the members the app acts on have to be in there
            foreach (string member in new[] { "documentSummary", "patientText", "newGroups", "toolAssignments", "paintings" })
            {
                Assert.IsNotNull(schema["properties"][member], $"'{member}' missing from the schema.");
            }
            foreach (string member in new[] { "findingText", "group", "toolName", "regionKeys", "description", "confidence" })
            {
                Assert.IsNotNull(paintings["items"]["properties"][member], $"painting member '{member}' missing.");
            }
        }

        /// <summary>The two responses that were already in use keep working - they are flat, so
        /// the recursive builder has to produce the same simple shape for them.</summary>
        [Test]
        public void Schema_OfTheResponsesAlreadyInUse_StaysFlatAndStrict()
        {
            JObject injury = Schema<MedicalAI.InjuryDescriptionResponse>();
            AssertStrict(injury, "InjuryDescriptionResponse");
            Assert.AreEqual("string", injury["properties"]["description"]["type"].ToString());
            Assert.AreEqual("string", injury["properties"]["category"]["type"].ToString());
            Assert.AreEqual(2, ((JObject)injury["properties"]).Count);

            JObject summary = Schema<MedicalAI.PatientSummaryResponse>();
            AssertStrict(summary, "PatientSummaryResponse");
            Assert.AreEqual("string", summary["properties"]["description"]["type"].ToString());
            Assert.AreEqual("string", summary["properties"]["condition"]["type"].ToString());
            Assert.AreEqual(2, ((JObject)summary["properties"]).Count);
        }

        [Test]
        public void Format_WrapsTheSchemaTheWayTheApiWantsIt()
        {
            JObject format = JObject.Parse(JsonConvert.SerializeObject(JsonSchemaBuilder.Format(typeof(Leaf))));
            Assert.AreEqual("json_schema", format["type"].ToString());
            Assert.AreEqual("Leaf", format["name"].ToString());
            Assert.IsTrue(format["strict"].ToObject<bool>(), "Strict mode is what we rely on.");
            Assert.AreEqual("object", format["schema"]["type"].ToString());
        }

        [Test]
        public void Schema_OfASelfReferencingType_FailsInsteadOfHanging()
        {
            Assert.Throws<System.InvalidOperationException>(() => Schema<SelfReferencing>());
        }

        /// <summary>Walks the whole schema and checks what strict mode demands of every object.</summary>
        private static void AssertStrict(JToken node, string path)
        {
            string type = node["type"] != null ? node["type"].ToString() : "";

            if (type == "object")
            {
                var properties = node["properties"] as JObject;
                Assert.IsNotNull(properties, $"{path}: an object without properties.");
                Assert.IsNotNull(node["additionalProperties"], $"{path}: additionalProperties missing.");
                Assert.IsFalse(node["additionalProperties"].ToObject<bool>(),
                    $"{path}: additionalProperties has to be false in strict mode.");

                var required = new List<string>(Strings(node["required"]));
                foreach (var property in properties)
                {
                    Assert.Contains(property.Key, required,
                        $"{path}: '{property.Key}' is not in required, which strict mode does not allow.");
                    AssertStrict(property.Value, path + "." + property.Key);
                }
                Assert.AreEqual(properties.Count, required.Count, $"{path}: required lists members that do not exist.");
            }
            else if (type == "array")
            {
                Assert.IsNotNull(node["items"], $"{path}: an array without items.");
                AssertStrict(node["items"], path + "[]");
            }
            else
            {
                Assert.IsNotEmpty(type, $"{path}: no type at all.");
            }
        }

        private static List<string> Strings(JToken array)
        {
            var values = new List<string>();
            if (array != null)
            {
                foreach (JToken value in array) values.Add(value.ToString());
            }
            return values;
        }
    }
}
