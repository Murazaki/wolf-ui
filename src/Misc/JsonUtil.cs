using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace WolfUI.Misc;

public sealed class OptInJsonTypeInfoResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        List<JsonPropertyInfo> propToRemove = [.. jsonTypeInfo.Properties
                .Where(prop => prop.AttributeProvider is not null 
                               && !prop.AttributeProvider.IsDefined(typeof(JsonIncludeAttribute), false))];

        foreach (var prop in propToRemove)
        {
            jsonTypeInfo.Properties.Remove(prop);
        }

        return jsonTypeInfo;
    }
}