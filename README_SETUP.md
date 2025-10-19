# ?? AI Travel Planner - Instrukcje konfiguracji

## ?? Spos�b 1: U�yj pliku .env (Zalecane)

1. **Otw�rz plik `.env`** w katalogu g��wnym projektu
2. **Zdob�d� klucz API OpenAI:**
   - Przejd� do: https://platform.openai.com/api-keys
   - Zaloguj si� lub za�� konto
   - Kliknij "Create new secret key"
   - Skopiuj klucz (zaczyna si� od `sk-proj-...`)

3. **Zamie� lini� w pliku `.env`:**
   ```
   OPENAI_API_KEY=your_openai_api_key_here
   ```
   
   **Na:**
   ```
   OPENAI_API_KEY=sk-proj-1234567890abcdef1234567890abcdef1234567890abcdef
   ```
   *(u�yj swojego prawdziwego klucza)*

4. **Zapisz plik i uruchom aplikacj�:**
   ```bash
   dotnet run
   ```

## ?? Spos�b 2: Zmienne �rodowiskowe systemowe

Alternatywnie, mo�esz ustawi� zmienn� �rodowiskow� systemow�:

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

Po uruchomieniu aplikacji powiniene� zobaczy� jeden z komunikat�w:

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

## ?? Bezpiecze�stwo

- **NIGDY nie commituj pliku `.env` do gita** (ju� dodany do `.gitignore`)
- **NIGDY nie udost�pniaj publicznie swojego klucza API**
- **Klucz API to sekret** - traktuj go jak has�o

## ?? Koszty API

- OpenAI API jest **p�atne** (ale bardzo tanie)
- GPT-3.5-turbo kosztuje oko�o $0.002 za 1000 token�w
- Jeden plan podr�y = oko�o $0.01-0.05
- Ustaw limity bud�etu na https://platform.openai.com/account/billing

## ??? Rozwi�zywanie problem�w

**Problem:** "OpenAI API key is not configured"
**Rozwi�zanie:** Sprawd� czy klucz API jest poprawnie ustawiony w `.env`

**Problem:** "Unauthorized" lub b��d 401
**Rozwi�zanie:** Sprawd� czy klucz API jest poprawny i aktywny

**Problem:** Formularz nie dzia�a
**Rozwi�zanie:** Sprawd� logi aplikacji (dotnet run) i poszukaj szczeg��w b��d�w

## ?? Tryb Demo

Je�li nie chcesz u�ywa� prawdziwego AI, aplikacja automatycznie prze��czy si� w **tryb demo** z symulowanymi odpowiedziami.