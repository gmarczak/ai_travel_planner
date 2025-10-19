# ?? AI Travel Planner - Instrukcje konfiguracji

## ?? Sposób 1: U¿yj pliku .env (Zalecane)

1. **Otwórz plik `.env`** w katalogu g³ównym projektu
2. **Zdob¹dŸ klucz API OpenAI:**
   - PrzejdŸ do: https://platform.openai.com/api-keys
   - Zaloguj siê lub za³ó¿ konto
   - Kliknij "Create new secret key"
   - Skopiuj klucz (zaczyna siê od `sk-proj-...`)

3. **Zamieñ liniê w pliku `.env`:**
   ```
   OPENAI_API_KEY=your_openai_api_key_here
   ```
   
   **Na:**
   ```
   OPENAI_API_KEY=sk-proj-1234567890abcdef1234567890abcdef1234567890abcdef
   ```
   *(u¿yj swojego prawdziwego klucza)*

4. **Zapisz plik i uruchom aplikacjê:**
   ```bash
   dotnet run
   ```

## ?? Sposób 2: Zmienne œrodowiskowe systemowe

Alternatywnie, mo¿esz ustawiæ zmienn¹ œrodowiskow¹ systemow¹:

**Windows (PowerShell):**
```powershell
$env:OPENAI_API_KEY="your_api_key_here"
dotnet run
```

**Windows (CMD):**
```cmd
set OPENAI_API_KEY=your_api_key_here
dotnet run
```

**Linux/Mac:**
```bash
export OPENAI_API_KEY="your_api_key_here"
dotnet run
```

## ? Sprawdzenie konfiguracji

Po uruchomieniu aplikacji powinieneœ zobaczyæ jeden z komunikatów:

**? Poprawnie skonfigurowane:**
```
?? Using OpenAI Travel Service
? AI Travel Planner started with OpenAI integration
?? API Key configured successfully
```

**?? Brak klucza API (tryb demo):**
```
?? Using Mock Travel Service (no API key provided)
??  AI Travel Planner started in DEMO mode
?? To use real AI, add your OpenAI API key to the .env file
?? Instructions: https://platform.openai.com/api-keys
```

## ?? Bezpieczeñstwo

- **NIGDY nie commituj pliku `.env` do gita** (ju¿ dodany do `.gitignore`)
- **NIGDY nie udostêpniaj publicznie swojego klucza API**
- **Klucz API to sekret** - traktuj go jak has³o

## ?? Koszty API

- OpenAI API jest **p³atne** (ale bardzo tanie)
- GPT-3.5-turbo kosztuje oko³o $0.002 za 1000 tokenów
- Jeden plan podró¿y = oko³o $0.01-0.05
- Ustaw limity bud¿etu na https://platform.openai.com/account/billing

## ??? Rozwi¹zywanie problemów

**Problem:** "OpenAI API key is not configured"
**Rozwi¹zanie:** SprawdŸ czy klucz API jest poprawnie ustawiony w `.env`

**Problem:** "Unauthorized" lub b³¹d 401
**Rozwi¹zanie:** SprawdŸ czy klucz API jest poprawny i aktywny

**Problem:** Formularz nie dzia³a
**Rozwi¹zanie:** SprawdŸ logi aplikacji (dotnet run) i poszukaj szczegó³ów b³êdów

## ?? Tryb Demo

Jeœli nie chcesz u¿ywaæ prawdziwego AI, aplikacja automatycznie prze³¹czy siê w **tryb demo** z symulowanymi odpowiedziami.