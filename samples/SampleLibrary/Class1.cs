using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Bson.Converters;
using Newtonsoft.Json.Bson.Utilities;

namespace SampleLibrary
{
    public class Class1
    {
        public Class1()
        {
            var p = new JsonException();
            
            var x = new BsonDataRegexConverter();
        }
    }
}
