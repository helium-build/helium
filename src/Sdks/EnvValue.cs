using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Helium.Sdks
{
    [JsonConverter(typeof(EnvValue.EnvValueConverter))]
    public abstract class EnvValue
    {
        private EnvValue() {}
        
        protected abstract JToken ToToken(JsonSerializer serializer);

        public abstract string Resolve(string sdkDirectory);

        public sealed class OfString : EnvValue
        {
            public OfString(string value) {
                Value = value;
            }
            
            public string Value { get; }

            protected override JToken ToToken(JsonSerializer serializer) => new JValue(Value);
            public override string Resolve(string sdkDirectory) => Value;
        }

        public sealed class Concat : EnvValue
        {
            public Concat(IEnumerable<EnvValue> values) {
                Values = values.ToList().AsReadOnly();
            }
            
            public IReadOnlyList<EnvValue> Values { get; }

            protected override JToken ToToken(JsonSerializer serializer) =>
                new JArray(Values.Select(x => (object)JToken.FromObject(Values, serializer)).ToArray());
            public override string Resolve(string sdkDirectory) => string.Concat(Values.Select(value => value.Resolve(sdkDirectory)));
        }

        [JsonConverter(typeof(JsonSubTypes.JsonSubtypes))]
        [JsonSubtypes.KnownSubTypeWithProperty(typeof(BuiltInValue), nameof(BuiltInValue.Name))]
        public abstract class ObjEnvValue : EnvValue
        {
            protected override JToken ToToken(JsonSerializer serializer) =>
                JToken.FromObject(this, serializer);
        }

        public sealed class BuiltInValue : ObjEnvValue
        {
            public BuiltInValue(string name) {
                Name = name;
            }
            
            public string Name { get; }

            public const string SdkDirectory = "SdkDirectory";
            public override string Resolve(string sdkDirectory) => sdkDirectory;
        }
        
        public class EnvValueConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
                var envValue = (EnvValue?)value ?? throw new ArgumentNullException(nameof(value));
                envValue.ToToken(serializer).WriteTo(writer);
            }
            
            public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
                switch(reader.TokenType) {
                    case JsonToken.String:
                    {
                        var strValue = (string?)reader.Value ?? throw new Exception("String value was null");
                        return new OfString(strValue);
                    }
                    case JsonToken.StartArray:
                    {
                        var elems = JArray.Load(reader).Select(x => x.ToObject<EnvValue>(serializer) ?? throw new Exception("Array element was null"));
                        return new Concat(elems);
                    }
                        
                    case JsonToken.StartObject:
                        return JObject.Load(reader).ToObject<ObjEnvValue>(serializer) ?? throw new Exception("Object value was null.");
                    
                    default:
                        throw new Exception("Could not read env value");
                }
            }

            public override bool CanConvert(Type objectType) =>
                objectType == typeof(EnvValue);
        }
    }
}