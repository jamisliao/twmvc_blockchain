using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Blocto.Sdk.Core.Utility;

public class JsonNetSerializer
    {
        public string SerializeToString<T>(T input)
        {
            string result;
            using (var ms = new MemoryStream())
            {
                SerializerToStream(input, ms);
                result = Encoding.UTF8.GetString(ms.ToArray());
            }

            return result;
        }

        private void SerializerToStream<T>(T input, Stream ms)
        {
            using (var writer = new StreamWriter(ms, Encoding.UTF8))
            using (var json = new JsonTextWriter(writer))
            {
                var ser = new JsonSerializer
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                    DateFormatString = "yyyy-MM-dd"
                };

                ser.Serialize(json, input);
                ms.Seek(0, SeekOrigin.Begin);
            }
        }

        public T DeserializeFormString<T>(string input)
        {
            T result;
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(input)))
            using (var reader = new StreamReader(ms, Encoding.UTF8))
            using (var json = new JsonTextReader(reader))
            {
                var ser = new JsonSerializer
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                result = ser.Deserialize<T>(json);
            }

            return result;
        }

        public T DeserializeFormStream<T>(Stream input)
        {
            T result;
            using (var reader = new StreamReader(input, Encoding.UTF8))
            using (var json = new JsonTextReader(reader))
            {
                var ser = new JsonSerializer
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                result = ser.Deserialize<T>(json);
            }

            return result;
        }
    }