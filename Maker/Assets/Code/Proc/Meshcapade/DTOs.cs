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
        // possible capitalized/camel fields seen in different responses
        [JsonProperty("Link", NullValueHandling = NullValueHandling.Ignore)]
        public string Link;

        [JsonProperty("link", NullValueHandling = NullValueHandling.Ignore)]
        public string link;

        // new: explicit fields your JSON used
        [JsonProperty("path", NullValueHandling = NullValueHandling.Ignore)]
        public string path;

        [JsonProperty("Method", NullValueHandling = NullValueHandling.Ignore)]
        public string Method;

        [JsonProperty("method", NullValueHandling = NullValueHandling.Ignore)]
        public string method;

        [JsonProperty("internal", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Internal;

        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData;

        // returns whichever string field is available (path, Link, link, or any string value in AdditionalData)
        public string GetLink()
        {
            if (!string.IsNullOrEmpty(path)) return path;
            if (!string.IsNullOrEmpty(Link)) return Link;
            if (!string.IsNullOrEmpty(link)) return link;

            if (AdditionalData != null)
            {
                // Try common alternatives
                if (AdditionalData.TryGetValue("path", out var tkp) && tkp.Type == JTokenType.String) return tkp.ToString();
                if (AdditionalData.TryGetValue("Link", out var tkL) && tkL.Type == JTokenType.String) return tkL.ToString();
                if (AdditionalData.TryGetValue("link", out var tkl) && tkl.Type == JTokenType.String) return tkl.ToString();
            }

            return null;
        }

        // returns whichever method field is present
        public string GetMethod()
        {
            if (!string.IsNullOrEmpty(method)) return method;
            if (!string.IsNullOrEmpty(Method)) return Method;
            if (AdditionalData != null && AdditionalData.TryGetValue("method", out var tk) && tk.Type == JTokenType.String) return tk.ToString();
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

    public class ShapeParameters
    {
        /*[JsonProperty("Height")] public string Height;
        [JsonProperty("Weight")] public string Weight;
        [JsonProperty("Bust_girth")] public string Bust_girth;
        [JsonProperty("Ankle_girth")] public string Ankle_girth;
        [JsonProperty("Inside_leg_height")] public float Inside_leg_height;*/
        [JsonProperty("Height")]
        public string Height { get; set; }

        [JsonProperty("Weight")]
        public string Weight { get; set; }

        [JsonProperty("Bust_girth")]
        public string Bust_girth { get; set; }

        [JsonProperty("Ankle_girth")]
        public string Ankle_girth { get; set; }

        [JsonProperty("Inside_leg_height")]
        public float Inside_leg_height { get; set; }
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
        [JsonProperty("url")] public UrlObject url; // if present
    }

    // 9) Generic error DTO you can use for logging or to display nice messages
    public class JsonApiError
    {
        [JsonProperty("status")] public string status;
        [JsonProperty("code")] public string code;
        [JsonProperty("title")] public string title;
        [JsonProperty("detail")] public string detail;
    }
    
    public class FinalExportRequestBody
    {
        public ExportRequestData data { get; set; }
    }

    public class ExportRequestData
    {
        public string type { get; set; } = "export";
        public ExportRequestAttributes attributes { get; set; }
    }

    public class ExportRequestAttributes
    {
        public string filename { get; set; }
        public string format { get; set; } = "OBJ"; // or "OBJ", "FBX"
        public string pose { get; set; } = "scan";
    }

    public class FinalFittingImageBody
    {
        public FittingImageData data { get; set; }
    }

    public class FittingImageData
    {
        public string type { get; set; } = "fit-to-images";
        public FittingImageAttributes attributes { get; set; }
    }

    public class FittingImageAttributes
    {
        public string avatarname { get; set; } = "meerjungfrau2";
        public string gender { get; set; } = "female";
        public string imageMode { get; set; } = "AFI";
    }

}

