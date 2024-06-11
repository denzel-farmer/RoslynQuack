namespace SerializeBasicHelpers
{
    public class OtherWeatherForecastChild : SerializeBasic.WeatherForecast
    {
        public int OtherInt { get; set; }
    }
    internal class InternalWeatherForecastChild : SerializeBasic.WeatherForecast
    {
        public int InternalInt { get; set; }
    }
}