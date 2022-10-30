﻿using System;
using Flow.Net.Sdk.Core.Cadence.Types;
using Flow.Net.Sdk.Core.Exceptions;
using Newtonsoft.Json;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Flow.Net.Sdk.Core.Cadence
{
    public abstract class Cadence : ICadence
    {
        public Cadence()
        {
            TempId = GetUniqueKey(10).ToLower();
        }
        
        public virtual string Type { get; set; }
        
        [JsonIgnore]
        public virtual string TempId { get; }

        /// <summary>
        /// Encodes <see cref="ICadence"/>.
        /// </summary>
        /// <param name="cadence"></param>
        /// <returns>A JSON string representation of <see cref="ICadence"/>.</returns>
        public string Encode(ICadence cadence)
        {
            JsonConverter[] jsonConverters = { new CadenceRepeatedTypeConverter(), new CadenceTypeValueAsStringConverter() };
            return JsonConvert.SerializeObject(cadence, jsonConverters);
        }

        /// <summary>
        /// Filters <see cref="CadenceCompositeItem.Fields"/> where <see cref="CadenceCompositeItemValue.Name"/> is equal to <paramref name="fieldName"/> and returns the <see cref="CadenceCompositeItemValue.Value"/>.
        /// </summary>
        /// <param name="cadenceComposite"></param>
        /// <param name="fieldName"></param>
        /// <returns>A <see cref="ICadence"/> that satisfies the condition.</returns>
        public ICadence CompositeField(CadenceComposite cadenceComposite, string fieldName)
        {
            var cadenceCompositeValue = cadenceComposite.Value.Fields.Where(w => w.Name == fieldName).Select(s => s.Value).FirstOrDefault();

            return cadenceCompositeValue ?? throw new FlowException($"Failed to find fieldName: {fieldName}");
        }

        /// <summary>
        /// Filters <see cref="CadenceCompositeItem.Fields"/> where <see cref="CadenceCompositeItemValue.Name"/> is equal to <paramref name="fieldName"/> and returns the <see cref="CadenceCompositeItemValue.Value"/> as <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cadenceComposite"></param>
        /// <param name="fieldName"></param>
        /// <returns>A <typeparamref name="T"/> that satisfies the condition.</returns>
        public T CompositeFieldAs<T>(CadenceComposite cadenceComposite, string fieldName)
            where T : ICadence
        {
            return cadenceComposite.CompositeField(fieldName).As<T>();
        }
        
        private string GetUniqueKey(int size)
        {
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            var data = new byte[4 * size];
            using (var crypto = RandomNumberGenerator.Create())
            {
                crypto.GetBytes(data);
            }
            
            var  result = new StringBuilder(size);
            for (var i = 0; i < size; i++)
            {
                var rnd = BitConverter.ToUInt32(data, i * 4);
                var idx = rnd % chars.Length;

                result.Append(chars[idx]);
            }

            return result.ToString();
        }
    }
}
