# Travel Planner

Prosta aplikacja do generowania planów podróży.

## Jak uruchomić

1. Zbuduj projekt:
   dotnet build
2. Uruchom aplikację:
   dotnet run

## Zrzuty ekranu


## Technologia

Aplikacja napisana w technologii ASP.NET Core Razor Pages (.NET 8). Frontend korzysta z HTML, CSS, JavaScript oraz SignalR do komunikacji w czasie rzeczywistym.

## Konfiguracja kluczy API i sekretów

Wrażliwe dane (np. klucze API do usług AI) przechowuj w:
- pliku `.env`
- systemie [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- zmiennych środowiskowych (na produkcji)

Przykład ustawienia klucza przez User Secrets:

```
dotnet user-secrets set "OpenAI:ApiKey" "twoj-klucz-api"
```

Klucze nie powinny być commitowane do repozytorium!
