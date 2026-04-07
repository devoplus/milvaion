using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Devoplus.JobForge.Core.Utilities;

public class ObjectIdConverter : JsonConverter
{
#pragma warning disable CS8765 // Buradaki dönüş tipi object? yapıldığında Newtonsoft.Json'da override edilemediği için değiştirilemiyor.
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
#pragma warning restore CS8765
    {
        if (objectType == typeof(MongoDB.Bson.ObjectId) || objectType == typeof(MongoDB.Bson.ObjectId?))
        {
            if (reader.TokenType != JsonToken.String)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return MongoDB.Bson.ObjectId.Empty;
                }

                throw new Exception($"Unexpected token parsing ObjectId. Expected String, got {reader.TokenType}.");
            }

            var value = (string)reader.Value;
            return string.IsNullOrEmpty(value) ? MongoDB.Bson.ObjectId.Empty : new MongoDB.Bson.ObjectId(value);
        }
        else if (objectType == typeof(List<MongoDB.Bson.ObjectId>))
        {
            List<MongoDB.Bson.ObjectId> result = new List<MongoDB.Bson.ObjectId>();
            var readToken = reader.TokenType.ToString();
            if (readToken != "Null")
            {
                JArray value = JArray.Load(reader);

                if (value != null)
                {
                    foreach (var item in value)
                    {
                        result.Add(string.IsNullOrEmpty(item.ToString()) ? MongoDB.Bson.ObjectId.Empty : new MongoDB.Bson.ObjectId(item.ToString()));
                    }

                    return result;
                }

#pragma warning disable CS8603 // Buradaki dönüş tipi object? yapılamadığı için warning üretiyor.
                return null;
            }
            else
            {
                return null;
            }
#pragma warning restore CS8603
        }
        else
        {
            throw new Exception("Object type mismatch.");
        }
    }

#pragma warning disable CS8765 // Buradaki value parametresi object? yapıldığında Newtonsoft.Json'da override edilemediği için değiştirilemiyor.
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
#pragma warning restore CS8765
    {
        if (value is MongoDB.Bson.ObjectId)
        {
            var objectId = (MongoDB.Bson.ObjectId)value;
            writer.WriteValue(objectId != MongoDB.Bson.ObjectId.Empty ? objectId.ToString() : string.Empty);
        }
        else if (value is List<MongoDB.Bson.ObjectId>)
        {
            List<string> result = new List<string>();

            foreach (var objectId in (List<MongoDB.Bson.ObjectId>)value)
            {
                result.Add(objectId != MongoDB.Bson.ObjectId.Empty ? objectId.ToString() : string.Empty);
            }
            if (result.Count != 0)
            {
                writer.WriteRawValue(JsonConvert.SerializeObject(result));
            }
            else
            {
                writer.WriteRawValue(JsonConvert.SerializeObject(new List<string>()));
            }
        }
        else
        {
            throw new Exception("Expected ObjectId value.");
        }
    }

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(MongoDB.Bson.ObjectId) || objectType == typeof(List<MongoDB.Bson.ObjectId>);
    }
}

