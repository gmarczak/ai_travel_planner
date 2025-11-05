# Travel Planner

Prosta aplikacja do generowania planów podróży.

## Jak uruchomić

1. Zbuduj projekt:
   dotnet build
2. Uruchom aplikację:
   dotnet run

## Zrzuty ekranu
<img width="1920" height="1080" alt="p1" src="https://github.com/user-attachments/assets/242be224-e907-4d5e-b96d-b77e22415a1c" />
<img width="1920" height="1080" alt="p2" src="https://github.com/user-attachments/assets/a08be833-6bed-45fe-a094-21bd8a00e979" />
<img width="1920" height="1080" alt="p3" src="https://github.com/user-attachments/assets/4b813f91-7a8c-4083-ba74-d7bccd838674" />
<img width="1920" height="1080" alt="p4" src="https://github.com/user-attachments/assets/db004fb1-7960-49ce-bd67-ef45a66fe3ae" />
<img width="1920" height="1080" alt="p5" src="https://github.com/user-attachments/assets/44f31504-2c89-48d6-8192-d76cf6cf0fb5" />
<img width="1920" height="1080" alt="p6" src="https://github.com/user-attachments/assets/8019a5cd-0e59-43a8-a3f2-c92405a08497" />
<img width="1920" height="1080" alt="p7" src="https://github.com/user-attachments/assets/4e817c9b-c1ba-4c1c-83e6-176d49782bcb" />
<img width="1920" height="1080" alt="p8" src="https://github.com/user-attachments/assets/f7415c87-7d60-437e-a8fa-879f194dbb04" />
<img width="1920" height="1080" alt="p9" src="https://github.com/user-attachments/assets/7a61366b-df30-433e-ab41-ddc074acb759" />
<img width="1920" height="1080" alt="p10" src="https://github.com/user-attachments/assets/b13bea3e-7e95-4126-8039-056a568ef78c" />


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
