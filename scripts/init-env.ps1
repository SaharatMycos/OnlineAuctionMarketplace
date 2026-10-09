<#
.SYNOPSIS
    Creates the git-ignored .env from .env.example, filling every "change-me" value with a random secret.

.DESCRIPTION
    Run once per machine. docker compose and the .NET hosts (dotnet run / dotnet ef) both read .env.
    Refuses to overwrite an existing .env unless -Force is given.

    Note: Postgres only applies POSTGRES_PASSWORD when its data volume is first created. If the
    pgdata volume already exists, either reset it (docker compose down -v, which deletes the data)
    or set the new password on the running database:
        docker compose exec postgres psql -U marketplace -d marketplace -c "ALTER USER marketplace PASSWORD '<new>'"

.EXAMPLE
    pwsh -File scripts/init-env.ps1
#>
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$example = Join-Path $root '.env.example'
$target = Join-Path $root '.env'

if ((Test-Path $target) -and -not $Force) {
    Write-Host ".env already exists. Use -Force to regenerate it (new secrets)." -ForegroundColor Yellow
    exit 0
}

# Hex only: safe inside Npgsql and StackExchange.Redis connection strings.
function New-Secret([int]$bytes = 32) {
    $buffer = [byte[]]::new($bytes)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
    -join ($buffer | ForEach-Object { $_.ToString('x2') })
}

$lines = Get-Content $example | ForEach-Object {
    if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*change-me\s*$') { "$($Matches[1])=$(New-Secret)" } else { $_ }
}
# UTF-8 without BOM, LF endings, so docker compose and DotEnv parse it identically.
[System.IO.File]::WriteAllText($target, (($lines -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))

Write-Host "Created .env with new random secrets (git-ignored; don't commit it)." -ForegroundColor Green
