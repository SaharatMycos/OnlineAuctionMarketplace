<#
.SYNOPSIS
    Creates the local secrets: .env (for docker compose) and each host's appsettings.Development.json
    (for dotnet run / dotnet ef). Both are git-ignored.

.DESCRIPTION
    1. Creates .env from .env.example, filling every "change-me" value with a random secret.
       Keeps an existing .env unless -Force is given.
    2. Writes src/Marketplace.{Api,Realtime,Worker}/appsettings.Development.json from the values in
       .env, so compose and dotnet run share one set of secrets. These files are regenerated on every
       run: change values in .env, not in the JSON.

    The committed appsettings.json files hold no secrets; appsettings.Development.json overrides them
    in Development. Environment variables still win over both (compose, production).

    Postgres runs outside Docker, on POSTGRES_HOST (default localhost). Set POSTGRES_USER / POSTGRES_PASSWORD in .env to an
    existing login that may create databases and extensions; the api then creates the database and
    applies the EF Core migrations on startup. -Force generates a random POSTGRES_PASSWORD, so set
    it back to your login's password afterwards.

.EXAMPLE
    pwsh -File scripts/init-env.ps1
#>
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$example = Join-Path $root '.env.example'
$target = Join-Path $root '.env'
$utf8 = [System.Text.UTF8Encoding]::new($false)

# Hex only: safe inside Npgsql and StackExchange.Redis connection strings.
function New-Secret([int]$bytes = 32) {
    $buffer = [byte[]]::new($bytes)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
    -join ($buffer | ForEach-Object { $_.ToString('x2') })
}

if ((Test-Path $target) -and -not $Force) {
    Write-Host ".env already exists; keeping its secrets (use -Force to regenerate)." -ForegroundColor Yellow
} else {
    $lines = Get-Content $example | ForEach-Object {
        if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*change-me\s*$') { "$($Matches[1])=$(New-Secret)" } else { $_ }
    }
    # UTF-8 without BOM, LF endings, so docker compose parses it reliably.
    [System.IO.File]::WriteAllText($target, (($lines -join "`n") + "`n"), $utf8)
    Write-Host "Created .env with new random secrets (git-ignored; don't commit it)." -ForegroundColor Green
}

$values = @{}
foreach ($line in Get-Content $target) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $values[$Matches[1]] = $Matches[2] }
}
foreach ($name in 'POSTGRES_USER', 'POSTGRES_PASSWORD', 'POSTGRES_DB', 'REDIS_PASSWORD', 'AUTH_SIGNING_KEY') {
    if (-not $values[$name] -or $values[$name] -eq 'change-me') { throw "$name is missing in .env. Run with -Force to regenerate it." }
}
$port = if ($values.POSTGRES_PORT) { $values.POSTGRES_PORT } else { '5432' }
$pgHost = if ($values.POSTGRES_HOST) { $values.POSTGRES_HOST } else { 'localhost' }

$connectionStrings = [ordered]@{
    Postgres = "Host=$pgHost;Port=$port;Database=$($values.POSTGRES_DB);Username=$($values.POSTGRES_USER);Password=$($values.POSTGRES_PASSWORD)"
    Redis    = "localhost:6379,password=$($values.REDIS_PASSWORD)"
}
$cors = [ordered]@{ Origins = @('http://localhost:3000') }

$hosts = [ordered]@{
    'Marketplace.Api' = [ordered]@{
        ConnectionStrings = $connectionStrings
        Database          = [ordered]@{ MigrateOnStartup = $true }
        Auth              = [ordered]@{ SigningKey = $values.AUTH_SIGNING_KEY; DevTokens = $true }
        Cors              = $cors
    }
    'Marketplace.Realtime' = [ordered]@{
        ConnectionStrings = $connectionStrings
        Auth              = [ordered]@{ SigningKey = $values.AUTH_SIGNING_KEY }
        Cors              = $cors
    }
    'Marketplace.Worker' = [ordered]@{
        ConnectionStrings = $connectionStrings
    }
}

foreach ($project in $hosts.Keys) {
    $path = Join-Path $root "src/$project/appsettings.Development.json"
    $json = $hosts[$project] | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($path, $json + "`n", $utf8)
    Write-Host "Wrote src/$project/appsettings.Development.json (git-ignored)." -ForegroundColor Green
}
