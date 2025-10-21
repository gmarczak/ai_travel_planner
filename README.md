# 🌍 AI Travel Planner

AI-powered travel itinerary generator using OpenAI GPT-3.5-turbo and Google Maps.

## ✨ Features

- 🤖 AI-Generated Itineraries
- 🗺️ Interactive Maps with color-coded markers
- 💾 Save & Share Plans (anonymous or registered)
- 🔐 User Authentication
- ⚡ Background Processing

## 🚀 Quick Start

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- OpenAI API Key ([Get here](https://platform.openai.com/api-keys))
- Google Maps API Key ([Get here](https://console.cloud.google.com/apis/credentials))

### Installation

1. Clone and install:
   `ash
   git clone https://github.com/YOUR_USERNAME/ai-travel-planner.git
   cd ai-travel-planner
   dotnet restore
   `

2. Configure .env file:
   `env
   OPENAI_API_KEY=sk-proj-your-key-here
   GoogleMaps__ApiKey=AIzaSy-your-key-here
   `

3. Run:
   `ash
   dotnet ef database update
   dotnet run
   `

4. Open: http://localhost:5000

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

- ASP.NET Core 8.0 (Razor Pages)
- Entity Framework Core + SQLite
- OpenAI GPT-3.5-turbo
- Google Maps API
- Bootstrap 5.3.3

## 📁 Project Structure

`
project/
├── Pages/              # UI (Razor Pages)
├── Models/             # Data models
├── Services/           # Business logic & AI integration
├── Data/               # Database context
├── Migrations/         # EF Core migrations
└── wwwroot/            # Static files (CSS, JS)
`

## 🔒 Security

- Never commit .env file (in .gitignore)
- SQLite for development only
- Use SQL Server for production

## 📊 Recent Updates (Oct 2025)

- ✅ Database-only storage for anonymous users
- ✅ TempData alert system
- ✅ Map loading spinner & color-coded markers
- ✅ Improved InfoWindows

## 🎯 Key Features Explained

### 1. Anonymous User Support
- Plans saved with cookie-based `AnonymousCookieId`
- Automatic merge to user account on login/register
- 1-year cookie persistence
- No login required to start planning

### 2. Interactive Maps
- Color-coded markers per day (6 unique colors)
- "All Days" view shows all markers with different colors
- Individual day view shows numbered markers (1, 2, 3...)
- Drag & drop reordering of stops
- Live geocoding with Google Places API
- Polylines connecting stops in sequence

### 3. Background Processing
- Async plan generation with `PlanGenerationWorker`
- Real-time status updates via polling
- Prevents UI blocking during AI generation
- Queue-based job system for scalability

### 4. User Feedback System
- Bootstrap alert notifications
- 4 message types: Success, Error, Info, Warning
- TempData-based flash messages
- Auto-dismiss and manual close options

---

**Built with ❤️ using ASP.NET Core 8.0 and OpenAI GPT-3.5-turbo**
