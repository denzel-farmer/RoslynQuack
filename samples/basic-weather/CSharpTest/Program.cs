using System.Reflection;

namespace SerializeBasic
{

    public class EasyExploitGadget
    {
        public string? Name { get; set; }
        public string? Description { get; set; }

        public EasyExploitGadget(string name, string description)
        {
            Name = "Evil Gadget";
            Description = "Do evil things";
        }

        // tostring
        public override string ToString()
        {
            return "Evil Gadget";
        }
    }

    public class RadarInfo
    {
        public string? RadarType { get; set; }
        public int Range { get; set; }

        public RadarInfo(string radarType, int range)
        {
            RadarType = radarType;
            Range = range;
        }

        public string GetRadarType()
        {
            return RadarType;
        }

        // to string
        public override string ToString()
        {
            return "RadarInfo: " + RadarType + "-" + Range.ToString();
        }
    }

    public class WindForecast
    {
        public string? Direction { get; set; }
        public int Speed { get; set; }
    }

    public class DurangoWindForecast : WindForecast
    {
        public int FireNadoDanger { get; set; }
    }

    public class WeatherForecast
    {
        public DateTimeOffset Date { get; set; }
        public int TemperatureCelsius { get; set; }
        public string? Summary { get; set; }
        public WindForecast Wind { get; set; }
        // possible vulnerability: too broad of a type
        public System.Object RadarInfo { get; set; }
    }

    public class DurangoWeatherForecast : WeatherForecast
    {
        public int FireDanger { get; set; }
    }

    public class WeatherForecastSerializationBinder : Newtonsoft.Json.Serialization.ISerializationBinder
    {
        // List of allowed types 
        public static readonly string[] AllowedTypes = new string[]
        {
            "SerializeBasic.WeatherForecast",
            "SerializeBasic.DurangoWeatherForecast",
            "SerializeBasic.WindForecast",
            "SerializeBasic.DurangoWindForecast",
            "SerializeBasic.RadarInfo",
            "SerializeBasic.EasyExploitGadget" /* For testing purposes, allow EasyExploitGadget */
        };
        public Type BindToType(string assemblyName, string typeName)
        {
            Console.WriteLine($"BindToType: {assemblyName}, {typeName}");
            if (AllowedTypes.Contains(typeName))
            {
                return Type.GetType(typeName);
            }
            throw new Exception($"Unable to bind type {typeName}");
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            Console.WriteLine($"BindToName: {serializedType.FullName}");
            assemblyName = null;
            typeName = serializedType.FullName;
        }
    }

    public class Program
    {
        private static void FunMethod(int a = 0)
        {
            Console.WriteLine("FunMethod");
            // Shouldn't mess with finding symbol in main
            var deserializedWeatherForecast = 5;
            Console.WriteLine(deserializedWeatherForecast);
        }
        private static void FunMethod(string b = "bob")
        {
            Console.WriteLine("FunMethod2");
        }

        private static void TestDurangoReport(DurangoWeatherForecast report)
        {
            Console.WriteLine(report.FireDanger);
            Console.WriteLine(report.RadarInfo.ToString());
            Console.WriteLine(((RadarInfo)report.RadarInfo).GetRadarType());
        }
        public static void Main()
        {
            Console.WriteLine("Checking loaded classes");

            FunMethod(5);
            FunMethod("Hey");
            List<Type> list = new List<Type>();
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type t in ass.GetExportedTypes())
                {
                    list.Add(t);
                }
            }

            Object bob = "bob";

            var weatherForecast = new WeatherForecast
            {
                Date = DateTime.Parse("2019-08-01"),
                TemperatureCelsius = 25,
                Summary = "Hot",
                Wind = new WindForecast
                {
                    Direction = "North",
                    Speed = 10
                },
                RadarInfo = new RadarInfo("Big", 5)
            };
            Console.WriteLine("Serializing with System.Text.Json");
            string jsonString = System.Text.Json.JsonSerializer.Serialize(weatherForecast);

            Console.WriteLine(jsonString);

            Console.WriteLine("Serializing with Newtonsdoft.Json");
            jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(weatherForecast);
            Console.WriteLine(jsonString);

            Console.WriteLine("Serializing with Newtonsoft.Json and TypeHandling=All");
            jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(weatherForecast, new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All
            });

            Console.WriteLine(jsonString);

            Console.WriteLine("Deserializing with Newtonsoft.Json and TypeHandling=All and serialization binder");

            WeatherForecast? deserializedWeatherForecast;
            deserializedWeatherForecast = Newtonsoft.Json.JsonConvert.DeserializeObject<WeatherForecast>(jsonString, new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
                SerializationBinder = new WeatherForecastSerializationBinder()
            });

            var secondDeserializedWeatherForecast = deserializedWeatherForecast;
            var thirdDeserializedWeatherForecast = deserializedWeatherForecast;

            // Could be anything now
            MysteryBox(secondDeserializedWeatherForecast);

            Console.Write(deserializedWeatherForecast.Summary);
            Console.WriteLine(deserializedWeatherForecast.Wind.Direction);


            var durangoWeatherForecast = new DurangoWeatherForecast
            {
                Date = DateTime.Parse("2019-08-01"),
                TemperatureCelsius = 25,
                Summary = "Hot",
                Wind = new DurangoWindForecast
                {
                    Direction = "North",
                    Speed = 10,
                    FireNadoDanger = 10
                },
                FireDanger = 5,
                RadarInfo = new RadarInfo("Small", 2)
            };

            Console.WriteLine("Serializing Child with Newtonsoft.Json and TypeHandling=All");
            jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(durangoWeatherForecast, new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All
            });

            Console.WriteLine(jsonString);

            Console.WriteLine("Deserializing Child (cast as parent) with Newtonsoft.Json and TypeHandling=All and no serialization binder");
            var deserializedWeatherForecast2 = Newtonsoft.Json.JsonConvert.DeserializeObject<WeatherForecast>(jsonString, new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
                SerializationBinder = new WeatherForecastSerializationBinder()
            });

            var durangoDeserialized = (DurangoWeatherForecast)deserializedWeatherForecast2;

            Console.Write(durangoDeserialized.Summary);
            Console.WriteLine(durangoDeserialized.Wind.Direction);
            TestDurangoReport((DurangoWeatherForecast)durangoDeserialized);
            Console.WriteLine(((DurangoWeatherForecast)durangoDeserialized).FireDanger);


        }

        private static void MysteryBox(WeatherForecast? deserializedWeatherForecast)
        {
            // Assume this is some external API with an unkown implementation
            Console.Write(deserializedWeatherForecast);
        }
    }
}
// output:
//{"Date":"2019-08-01T00:00:00-07:00","TemperatureCelsius":25,"Summary":"Hot"}