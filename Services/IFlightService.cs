using project.Models;

namespace project.Services
{
    /// <summary>
    /// Interface for flight search services (Google Flights, Skyscanner, etc.)
    /// </summary>
    public interface IFlightService
    {
        /// <summary>
        /// Search for flights from departure to destination on specific dates
        /// </summary>
        Task<List<FlightOption>> SearchFlightsAsync(string departureCity, string destinationCity, DateTime departureDate, int passengers = 1);

        /// <summary>
        /// Build a booking link to flight scanners
        /// </summary>
        string GetGoogleFlightsUrl(string from, string to, DateTime departureDate);
        string GetSkyscannerUrl(string from, string to, DateTime departureDate);
        string GetKayakUrl(string from, string to, DateTime departureDate);
    }
}
