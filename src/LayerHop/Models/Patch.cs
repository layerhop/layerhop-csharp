#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LayerHop
{
    /// <summary>Builds a PATCH body that holds only the fields that were set.</summary>
    internal sealed class Patch
    {
        private readonly JObject _o = new JObject();

        internal JObject Body => _o;

        /// <summary>Sends the value when non-null.</summary>
        internal Patch Set(string key, string? v)
        {
            if (v != null) _o[key] = v;
            return this;
        }

        /// <summary>Sends the value when non-null; an empty string clears the field (JSON null).</summary>
        internal Patch Nullable(string key, string? v)
        {
            if (v == null) return this;
            _o[key] = v.Length == 0 ? JValue.CreateNull() : new JValue(v);
            return this;
        }

        internal Patch Set(string key, int? v)
        {
            if (v.HasValue) _o[key] = v.Value;
            return this;
        }

        internal Patch Set(string key, bool? v)
        {
            if (v.HasValue) _o[key] = v.Value;
            return this;
        }

        internal Patch Set(string key, JToken? v)
        {
            if (v != null) _o[key] = v;
            return this;
        }

        internal static JArray Strings(IEnumerable<string> values) => new JArray(values);
    }
}
