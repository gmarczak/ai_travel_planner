# 🌍 AI Travel Planner

AI-powered travel itinerary generator using OpenAI GPT and Google Maps. Plan your perfect trip with personalized recommendations for destinations, accommodations, activities, and transportation.

![AI Travel Planner](https://img.shields.io/badge/ASP.NET%20Core-8.0-blue)
![OpenAI](https://img.shields.io/badge/OpenAI-GPT--4-green)
![License](https://img.shields.io/badge/license-MIT-orange)

## ✨ Features

- 🤖 **AI-Generated Itineraries** - OpenAI GPT creates detailed day-by-day travel plans
- 🗺️ **Interactive Maps** - Google Maps integration with location pins and markers
- 👤 **User Authentication** - ASP.NET Core Identity for secure login/registration
- 💾 **Save & Manage Plans** - Store your travel plans and access them anytime
- 📱 **Responsive Design** - Beautiful Bootstrap 5 UI works on all devices
- 🎨 **Customizable** - Filter by budget, travel dates, preferences, and trip type

## 🚀 Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- OpenAI API Key ([Get one here](https://platform.openai.com/api-keys))
- Google Maps API Key ([Get one here](https://console.cloud.google.com/apis/credentials))

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/YOUR_USERNAME/ai-travel-planner.git
   cd ai-travel-planner
   ```

2. **Install dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure API keys**
   
   Copy `.env.example` to `.env` and add your API keys:
   ```bash
   cp .env.example .env
   ```
   
   Edit `.env`:
   ```env
   OPENAI_API_KEY=sk-proj-your-actual-key-here
   GoogleMaps__ApiKey=AIzaSy-your-actual-key-here
   ```

4. **Set up the database**
   ```bash
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

6. **Open your browser**
   ```
   http://localhost:5000
   ```

## 🔑 API Key Setup

### OpenAI API
1. Go to [OpenAI Platform](https://platform.openai.com/api-keys)
2. Sign in or create an account
3. Click "Create new secret key"
4. Copy the key and add it to your `.env` file

### Google Maps API
1. Go to [Google Cloud Console](https://console.cloud.google.com/apis/credentials)
2. Create a new project or select an existing one
3. Enable these APIs:
   - Maps JavaScript API
   - Places API
   - Geocoding API
4. Create credentials (API Key)
5. Copy the key and add it to your `.env` file

## 🏗️ Tech Stack

- **Backend**: ASP.NET Core 8.0 (Razor Pages)
- **Database**: Entity Framework Core + SQLite
- **Authentication**: ASP.NET Core Identity
- **AI**: OpenAI GPT-3.5/GPT-4
- **Maps**: Google Maps JavaScript API
- **Frontend**: Bootstrap 5.3, Vanilla JavaScript
- **Hosting**: Azure-ready (Azure App Service compatible)

## 📁 Project Structure

```
project/
├── Pages/                      # Razor Pages (Views + Controllers)
│   ├── Account/               # Login, Register, Logout
│   ├── TravelPlanner/         # Main travel planning pages
│   └── Shared/                # Layout, navigation
├── Models/                     # Data models
├── Services/                   # Business logic
│   ├── OpenAITravelService.cs # AI integration
│   └── Background/            # Background workers
├── Data/                       # Database context
├── wwwroot/                    # Static files (CSS, JS, images)
├── Migrations/                 # EF Core migrations
└── appsettings.json           # Configuration (WITHOUT secrets)
```

## 🎯 Usage

1. **Register/Login** - Create an account or sign in
2. **Plan a Trip** - Enter destination, dates, budget, and preferences
3. **Generate Itinerary** - AI creates a detailed day-by-day plan
4. **View on Map** - See all locations plotted on Google Maps
5. **Save Plan** - Save to your account for future reference
6. **Manage Plans** - View and manage all your saved trips

## 🔒 Security Notes

⚠️ **Never commit these files to Git:**
- `.env` - Contains your API keys
- `travelplanner.db` - Contains user data and passwords
- `appsettings.Development.json` - May contain secrets

✅ **Safe to commit:**
- All source code files (`.cs`, `.cshtml`)
- `appsettings.json` - Only with placeholder values
- `.env.example` - Template for environment variables

## 🚢 Deployment

### Azure App Service

1. Create an Azure App Service (ASP.NET Core 8.0)
2. Configure environment variables in Azure Portal:
   - `OPENAI_API_KEY`
   - `GoogleMaps__ApiKey`
3. Deploy using:
   ```bash
   dotnet publish -c Release
   ```
4. Upload to Azure or use GitHub Actions for CI/CD

### GitHub Actions CI/CD

Add secrets to your GitHub repository:
- `OPENAI_API_KEY`
- `GOOGLE_MAPS_API_KEY`
- `AZURE_WEBAPP_PUBLISH_PROFILE` (if deploying to Azure)

## 📝 Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `OPENAI_API_KEY` | OpenAI API key for GPT | ✅ Yes |
| `GoogleMaps__ApiKey` | Google Maps API key | ✅ Yes |
| `ANTHROPIC_API_KEY` | Claude API key (optional) | ❌ No |

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- OpenAI for GPT API
- Google Maps Platform
- ASP.NET Core team
- Bootstrap contributors

## 📧 Support

For issues and questions:
- Open an [Issue](https://github.com/YOUR_USERNAME/ai-travel-planner/issues)
- Check existing issues before creating a new one

---

**Made with ❤️ using ASP.NET Core and OpenAI**
