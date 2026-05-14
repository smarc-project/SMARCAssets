using System;
using Newtonsoft.Json;


namespace SmarcGUI.Connections
{
    /// <summary>
    /// {
    //   "alt": 11,
    //   "lat": 59.307144,
    //   "lon": 18.708915,
    //   "tst": 1686688800
    // }
    /// </summary>

    [JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.KebabCaseNamingStrategy))]
    public class OwntracksMsg
    {
        public double lat, lon;
        public float alt;
        public int tst;

        public OwntracksMsg() { }

        public OwntracksMsg(string jsonString)
        {
            JsonConvert.PopulateObject(jsonString, this);
        }

        public override string ToString()
        {
            return $"({lat}, {lon}): {alt} (Timestamp: {tst})";
        }
    }
}