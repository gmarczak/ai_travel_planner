# Heuristic script to find potentially unused files
# Usage: PowerShell -ExecutionPolicy Bypass -File .\tools\find-unused-files.ps1

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir
Write-Host "Scanning repository for potentially unused files under: $root" -ForegroundColor Cyan

# File types to consider
$exts = @('*.cs', '*.cshtml', '*.js', '*.css', '*.ts', '*.json')

# Collect all files
$allFiles = Get-ChildItem -Path $root -Recurse -File | Where-Object { $exts -contains ('*' + $_.Extension) }

# Build a content index (simple string search)
$index = @{}
foreach ($f in $allFiles) {
    $index[$f.FullName] = Get-Content -Raw -LiteralPath $f.FullName -ErrorAction SilentlyContinue
}

$candidates = @()
foreach ($f in $allFiles) {
    $name = $f.Name
    # skip obvious entry points
    if ($name -in @('Program.cs', 'Startup.cs', 'project.csproj', 'appsettings.json')) { continue }
    $occurrences = 0
    foreach ($kv in $index.GetEnumerator()) {
        if ($kv.Key -eq $f.FullName) { continue }
        if ($kv.Value -and $kv.Value.IndexOf($name, [System.StringComparison]::InvariantCultureIgnoreCase) -ge 0) { $occurrences++ }
    }
    if ($occurrences -eq 0) { $candidates += $f }
}

Write-Host "Potentially unused files:" -ForegroundColor Yellow
if ($candidates.Count -eq 0) { Write-Host "(none found)"; exit 0 }
$candidates | Sort-Object FullName | ForEach-Object { Write-Host $_.FullName }

Write-Host "\nNote: This is a heuristic. Please manually verify before deleting any files." -ForegroundColor Gray
