using project.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace project.Services
{
    /// <summary>
    /// Flight search service using Google Flights/Skyscanner APIs
    /// Provides flight options and booking links for travel plans
    /// </summary>
    public class FlightService : IFlightService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FlightService> _logger;

        public FlightService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<FlightService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<FlightOption>> SearchFlightsAsync(string departureCity, string destinationCity, DateTime departureDate, int passengers = 1)
        {
            if (string.IsNullOrWhiteSpace(departureCity) || string.IsNullOrWhiteSpace(destinationCity))
            {
                _logger.LogWarning("SearchFlightsAsync called with empty cities");
                return new List<FlightOption>();
            }

            try
            {
                // TODO: Implement actual Google Flights API or Skyscanner API call
                // For now, return mock flights to demonstrate structure
                return GenerateMockFlights(departureCity, destinationCity, departureDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search flights from {From} to {To}", departureCity, destinationCity);
                return new List<FlightOption>();
            }
        }

        /// <summary>
        /// Generate mock flight data for demonstration
        /// Replace with real API call once credentials are configured
        /// </summary>
        private List<FlightOption> GenerateMockFlights(string from, string to, DateTime departureDate)
        {
            var flights = new List<FlightOption>
            {
                new FlightOption
                {
                    Airline = "Lufthansa",
                    AirlineCode = "LH",
                    DepartureTime = departureDate.AddHours(8).AddMinutes(30),
                    ArrivalTime = departureDate.AddHours(11).AddMinutes(45),
                    DepartureAirport = "WAW",
                    DepartureAirportName = "Warsaw Chopin",
                    ArrivalAirport = "VIE",
                    ArrivalAirportName = "Vienna International",
                    Price = 120,
                    DurationMinutes = 195,
                    Stops = 0,
                    BookingUrl = GetGoogleFlightsUrl(from, to, departureDate)
                },
                new FlightOption
                {
                    Airline = "Ryanair",
                    AirlineCode = "FR",
                    DepartureTime = departureDate.AddHours(10),
                    ArrivalTime = departureDate.AddHours(12).AddMinutes(15),
                    DepartureAirport = "WAW",
                    DepartureAirportName = "Warsaw Chopin",
                    ArrivalAirport = "VIE",
                    ArrivalAirportName = "Vienna International",
                    Price = 45,
                    DurationMinutes = 135,
                    Stops = 0,
                    BookingUrl = GetGoogleFlightsUrl(from, to, departureDate)
                },
                new FlightOption
                {
                    Airline = "Austrian Airlines",
                    AirlineCode = "OS",
                    DepartureTime = departureDate.AddHours(14).AddMinutes(30),
                    ArrivalTime = departureDate.AddHours(17).AddMinutes(50),
                    DepartureAirport = "WAW",
                    DepartureAirportName = "Warsaw Chopin",
                    ArrivalAirport = "VIE",
                    ArrivalAirportName = "Vienna International",
                    Price = 135,
                    DurationMinutes = 200,
                    Stops = 1,
                    BookingUrl = GetGoogleFlightsUrl(from, to, departureDate)
                }
            };

            _logger.LogInformation("Generated {Count} mock flights from {From} to {To}", flights.Count, from, to);
            return flights;
        }

        public string GetGoogleFlightsUrl(string from, string to, DateTime departureDate)
        {
            var dateStr = departureDate.ToString("yyyy-MM-dd");
            return $"https://www.google.com/flights?q=flights+from+{Uri.EscapeDataString(from)}+to+{Uri.EscapeDataString(to)}+on+{dateStr}";
        }

        public string GetSkyscannerUrl(string from, string to, DateTime departureDate)
        {
            var dateStr = departureDate.ToString("dd/MM/yy");
            return $"https://www.skyscanner.com/transport/flights/{Uri.EscapeDataString(from)}/{Uri.EscapeDataString(to)}/{dateStr}/";
        }

        public string GetKayakUrl(string from, string to, DateTime departureDate)
        {
            var dateStr = departureDate.ToString("yyyy-MM-dd");
            return $"https://www.kayak.com/flights/{Uri.EscapeDataString(from)}-{Uri.EscapeDataString(to)}/{dateStr}";
        }
    }
}
