using Newtonsoft.Json;
using Newtonsoft.Json.Bson.Converters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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