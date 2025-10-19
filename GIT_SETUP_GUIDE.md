# 🚀 Git Setup & Deployment Guide

## ⚠️ BEFORE YOU COMMIT - SECURITY CHECKLIST

**STOP! Read this first before pushing to GitHub:**

### ✅ Files Already Protected (in .gitignore):
- ✅ `.env` - Your API keys
- ✅ `travelplanner.db` - User database
- ✅ `appsettings.Development.json` - Dev secrets
- ✅ `appsettings.Production.json` - Prod secrets
- ✅ `bin/` and `obj/` - Build artifacts
- ✅ `.vs/` - Visual Studio files
- ✅ `Data/savedPlans/*.json` - User travel plans

### 📝 Files Safe to Commit (cleaned of secrets):
- ✅ `appsettings.json` - Now contains placeholders only
- ✅ `.env.example` - Template for environment setup
- ✅ All source code (`.cs`, `.cshtml` files)
- ✅ `README.md` - Documentation
- ✅ `.gitignore` - Git ignore rules

---

## 🔧 Step 1: Install Git (if not installed)

Download from: https://git-scm.com/downloads

Or use winget:
```powershell
winget install Git.Git
```

Then restart PowerShell.

---

## 📦 Step 2: Initialize Git Repository

```powershell
# Navigate to project directory
cd C:\Users\marcz\Desktop\project\project

# Initialize Git repository
git init

# Add all files (respecting .gitignore)
git add .

# Check what will be committed (verify no secrets!)
git status

# Verify .env is NOT in the list!
```

---

## 🔍 Step 3: Verify No Secrets Are Staged

Run this command to ensure sensitive files are ignored:

```powershell
# Should show NOTHING (these files should be ignored)
git status | Select-String -Pattern "\.env|travelplanner\.db|appsettings\.Development"

# If any of these appear, DO NOT COMMIT!
```

---

## 💾 Step 4: Create First Commit

```powershell
# Configure Git (if first time)
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"

# Create first commit
git commit -m "Initial commit: AI Travel Planner with ASP.NET Core + OpenAI"
```

---

## 🌐 Step 5: Connect to GitHub

### Option A: Create New Repository on GitHub

1. Go to https://github.com/new
2. Repository name: `ai-travel-planner`
3. Description: "AI-powered travel itinerary generator using OpenAI GPT and Google Maps"
4. **DO NOT** initialize with README (we already have one)
5. Click "Create repository"

### Option B: Use GitHub CLI

```powershell
# Install GitHub CLI (if not installed)
winget install GitHub.cli

# Login to GitHub
gh auth login

# Create repository and push
gh repo create ai-travel-planner --public --source=. --remote=origin --push
```

---

## 📤 Step 6: Push to GitHub (Manual Method)

```powershell
# Add remote repository (replace YOUR_USERNAME)
git remote add origin https://github.com/YOUR_USERNAME/ai-travel-planner.git

# Verify remote
git remote -v

# Push to GitHub
git branch -M main
git push -u origin main
```

---

## 🔒 Step 7: Set Up GitHub Secrets (for CI/CD)

Go to your GitHub repository:
1. Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add these secrets:

| Secret Name | Value Source |
|-------------|--------------|
| `OPENAI_API_KEY` | From your `.env` file |
| `GOOGLE_MAPS_API_KEY` | From your `.env` file |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | (Optional) From Azure Portal |

---

## 🚢 Step 8: Deploy to Azure (Optional)

### Using Azure Portal:

1. Create Azure App Service (ASP.NET Core 8.0)
2. In Configuration → Application Settings, add:
   - `OPENAI_API_KEY`
   - `GoogleMaps__ApiKey`
3. In Deployment Center, connect to GitHub
4. Select your repository and branch
5. Azure will auto-deploy on every push!

### Using Azure CLI:

```powershell
# Install Azure CLI
winget install Microsoft.AzureCLI

# Login to Azure
az login

# Create resource group
az group create --name ai-travel-planner-rg --location eastus

# Create App Service plan
az appservice plan create --name ai-travel-planner-plan --resource-group ai-travel-planner-rg --sku B1 --is-linux

# Create web app
az webapp create --name YOUR-UNIQUE-NAME --resource-group ai-travel-planner-rg --plan ai-travel-planner-plan --runtime "DOTNETCORE:8.0"

# Configure environment variables
az webapp config appsettings set --name YOUR-UNIQUE-NAME --resource-group ai-travel-planner-rg --settings OPENAI_API_KEY="your-key" GoogleMaps__ApiKey="your-key"

# Deploy from GitHub
az webapp deployment source config --name YOUR-UNIQUE-NAME --resource-group ai-travel-planner-rg --repo-url https://github.com/YOUR_USERNAME/ai-travel-planner --branch main --manual-integration
```

---

## 🔄 Step 9: Regular Git Workflow

```powershell
# Check status
git status

# Add changes
git add .

# Commit
git commit -m "Description of changes"

# Push to GitHub
git push

# Pull latest changes
git pull
```

---

## 🛡️ Emergency: Accidentally Committed Secrets?

If you committed API keys by mistake:

```powershell
# Remove file from Git (but keep local copy)
git rm --cached .env

# Commit the removal
git commit -m "Remove .env from Git tracking"

# Force push (rewrites history)
git push -f origin main

# IMMEDIATELY rotate/regenerate your API keys!
# - OpenAI: https://platform.openai.com/api-keys
# - Google Maps: https://console.cloud.google.com/apis/credentials
```

---

## 📊 Verify Your Setup

Run this checklist:

```powershell
# ✅ .env is ignored
git check-ignore .env
# Should output: .env

# ✅ Database is ignored
git check-ignore travelplanner.db
# Should output: travelplanner.db

# ✅ appsettings.json has no real keys
Get-Content appsettings.json | Select-String "sk-proj|AIzaSy"
# Should return NOTHING

# ✅ .env.example exists
Test-Path .env.example
# Should return: True
```

---

## 🎉 You're Ready!

Your project is now:
- ✅ Protected from accidentally committing secrets
- ✅ Ready to push to GitHub
- ✅ Ready for Azure deployment
- ✅ Set up for CI/CD with GitHub Actions

---

## 📚 Additional Resources

- [Git Documentation](https://git-scm.com/doc)
- [GitHub Guides](https://guides.github.com/)
- [Azure App Service Docs](https://docs.microsoft.com/azure/app-service/)
- [Keeping secrets secure](https://docs.github.com/en/actions/security-guides/encrypted-secrets)

---

**Remember:** NEVER commit files containing real API keys! Always use environment variables and GitHub Secrets.
