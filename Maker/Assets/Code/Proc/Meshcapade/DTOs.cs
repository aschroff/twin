using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Code.Proc.Meshcapade
{
    // 1) Token response (you already have this; adding a few extras)
    public class TokenResponse
    {
        [JsonProperty("access_token")] public string access_token;
        [JsonProperty("expires_in")] public int expires_in;
        [JsonProperty("refresh_expires_in")] public int refresh_expires_in;
        [JsonProperty("token_type")] public string token_type;
        [JsonProperty("refresh_token")] public string refresh_token;
        [JsonProperty("scope")] public string scope;
    }

    // 2) Generic JSON:API top-level wrapper for single resource
    public class JsonApiSingle<T>
    {
        [JsonProperty("data")] public T data;
        [JsonProperty("meta")] public JToken meta;
        [JsonProperty("errors")] public JToken errors;
        [JsonProperty("links")] public JToken links;
    }

    // 3) Standard resource object (id/type/attributes/links)
    public class Resource<TAttrs>
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("type")] public string type;
        [JsonProperty("attributes")] public TAttrs attributes;
        [JsonProperty("links")] public Dictionary<string, string> links; // often includes "self" etc.
    }

    // 4) CreateAvatar response attributes (few fields you likely need)
    public class AvatarAttributes
    {
        // common fields — expand if you need more
        [JsonProperty("state")] public string state;          // e.g. processing state
        [JsonProperty("created_at")] public string created_at;
        [JsonProperty("updated_at")] public string updated_at;
    }

    // 5) Register image response attributes: the url object handed back on register
    //    Example JSON (typical shape):
    //    "attributes": {
    //       "url": { "Link": "<presigned-put-url>", "Method": "PUT", "Internal": false },
    //       "type": "IMAGE"
    //    }
    public class ImageRegisterAttributes
    {
        [JsonProperty("url")] public UrlObject url;
        [JsonProperty("type")] public string type; // e.g. "IMAGE"
        [JsonProperty("description")] public string description;
        [JsonProperty("created_at")] public string created_at;
    }

    public class UrlObject
    {
        // Some responses use "Link" (capitalized) as seen in docs/examples.
        [JsonProperty("Link", NullValueHandling = NullValueHandling.Ignore)]
        public string Link;

        // Some responses may use "link" lowercase — keep a fallback property for both:
        [JsonProperty("link", NullValueHandling = NullValueHandling.Ignore)]
        public string link;

        [JsonProperty("Method", NullValueHandling = NullValueHandling.Ignore)]
        public string Method;

        [JsonProperty("internal", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Internal;

        // In case the response is simply a string (rare but possible), keep raw token:
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData;
        
        // helper to get the actual link regardless of capitalization
        public string GetLink()
        {
            if (!string.IsNullOrEmpty(Link)) return Link;
            if (!string.IsNullOrEmpty(link)) return link;
            if (AdditionalData != null)
            {
                if (AdditionalData.TryGetValue("Link", out var tk) && tk.Type == JTokenType.String) return tk.ToString();
                if (AdditionalData.TryGetValue("link", out var tk2) && tk2.Type == JTokenType.String) return tk2.ToString();
            }
            return null;
        }
    }

    // 6) UploadInfo - normalized object your upload function will use
    public class UploadInfo
    {
        // final presigned PUT URL
        public string UploadUrl { get; set; }
        // recommended content type if available
        public string ContentType { get; set; }
        // optional: HTTP method required (usually PUT)
        public string Method { get; set; }
    }

    // 7) Avatar status response attributes (the data.attributes section)
    public class AvatarStatusAttributes
    {
        [JsonProperty("state")] public string state; // e.g. "PENDING", "PROCESSING", "READY", "FAILED"
        // If the API returns asset links in attributes or links
        [JsonProperty("asset_url")] public string asset_url;
        [JsonProperty("export")] public JToken export; // fallback for export sub-object
        [JsonProperty("shape_parameters")] public JToken shape_parameters;
    }

    // 8) Export job attributes (when you call /export)
    public class ExportRequestBody
    {
        [JsonProperty("pose")] public string pose;   // "scan" or other
        [JsonProperty("format")] public string format; // "obj" or "fbx" etc.
        [JsonProperty("textures")] public bool? textures; // optional
    }

    public class ExportAttributes
    {
        [JsonProperty("state")] public string state; // e.g. "PENDING", "READY", "FAILED"
        [JsonProperty("download")] public UrlObject download; // if present
    }

    // 9) Generic error DTO you can use for logging or to display nice messages
    public class JsonApiError
    {
        [JsonProperty("status")] public string status;
        [JsonProperty("code")] public string code;
        [JsonProperty("title")] public string title;
        [JsonProperty("detail")] public string detail;
    }
}

