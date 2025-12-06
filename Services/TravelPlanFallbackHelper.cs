namespace project.Services
{
    /// <summary>
    /// Provides fallback data for travel plans when AI services fail or return incomplete data.
    /// Centralized to avoid duplication across multiple AI service implementations.
    /// </summary>
    public static class TravelPlanFallbackHelper
    {
        public static List<string> GetFallbackAccommodations(string destination)
        {
            return new List<string>
            {
                $"Recommended Hotel in {destination}",
                $"Boutique Accommodation near {destination}",
                $"Budget-Friendly Option in {destination}",
                $"Luxury Resort in {destination}"
            };
        }

        public static List<string> GetFallbackActivities(string destination)
        {
            return new List<string>
            {
                $"City Tour of {destination}",
                $"Local Food Experience in {destination}",
                $"Historic Sites in {destination}",
                $"Shopping District in {destination}"
            };
        }

        public static List<string> GetFallbackTransportation()
        {
            return new List<string>
            {
                "Airport Transfer Service",
                "Public Transportation Pass",
                "Car Rental Options",
                "Taxi and Ride-sharing Services"
            };
        }

        public static string GetFallbackItinerary(string destination, DateTime startDate, DateTime endDate)
        {
            var days = (endDate - startDate).Days + 1;
            return $"🌟 {days}-Day Travel Plan for {destination}\n\n" +
                   $"We're experiencing technical difficulties with our AI service.\n" +
                   $"Please try again later for a fully personalized itinerary.\n\n" +
                   $"📅 {startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";
        }
    }
}
