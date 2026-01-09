namespace project.Models
{
    /// <summary>
    /// Flight option for display in Results page
    /// </summary>
    public class FlightOption
    {
        public string Airline { get; set; } = string.Empty;
        public string AirlineCode { get; set; } = string.Empty; // e.g., "LH" for Lufthansa
        public string AirlineLogoUrl { get; set; } = string.Empty;
        
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        
        public string DepartureAirport { get; set; } = string.Empty; // e.g., "WAW"
        public string DepartureAirportName { get; set; } = string.Empty; // e.g., "Warsaw Chopin"
        
        public string ArrivalAirport { get; set; } = string.Empty; // e.g., "VIE"
        public string ArrivalAirportName { get; set; } = string.Empty; // e.g., "Vienna International"
        
        public decimal Price { get; set; }
        public string PriceCurrency { get; set; } = "USD";
        
        public int DurationMinutes { get; set; }
        public int Stops { get; set; } // 0 = direct, 1+ = connections
        
        public bool IsDirect => Stops == 0;
        
        public string? BookingUrl { get; set; }
        
        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

        public string FormattedDuration
        {
            get
            {
                int hours = DurationMinutes / 60;
                int mins = DurationMinutes % 60;
                return $"{hours}h {mins}m";
            }
        }

        public string FormattedPrice => $"{PriceCurrency} {Price:N0}";
    }
}
