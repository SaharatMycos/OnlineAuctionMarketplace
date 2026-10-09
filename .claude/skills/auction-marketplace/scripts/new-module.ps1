<#
.SYNOPSIS
  Adds a new module to the modular monolith (EF Core code-first).
.DESCRIPTION
  Creates src/Modules/Marketplace.Modules.<Name> with:
    <Name>Module.cs                       public entry point (Name = schema, registers the DbContext)
    Persistence/<Name>DbContext.cs        code-first model of the "<name>" schema (outbox included)
    Persistence/<Name>DbContextFactory.cs design-time factory for dotnet ef
  then adds it to Marketplace.slnx, references it from Marketplace.Bootstrap, registers it in
  ModuleCatalog.All and creates the InitialCreate migration (schema + outbox table).
  -FilesOnly rewrites just the source files of an existing module (no solution/catalog/migration changes).
.EXAMPLE
  pwsh -File .claude/skills/auction-marketplace/scripts/new-module.ps1 -Name Disputes -Summary "Disputes: ..."
#>
param(
  [Parameter(Mandatory)][ValidatePattern('^[A-Z][A-Za-z]+$')][string]$Name,
  [string]$Summary = "$Name module.",
  [switch]$FilesOnly,
  # Only add Marketplace.Modules.<Name>.Contracts (public events/interfaces) to an existing module.
  [switch]$Contracts,
  [string]$Root = (Resolve-Path "$PSScriptRoot/../../../..").Path
)
$ErrorActionPreference = 'Stop'

if ($Contracts) {
  $cproj = "Marketplace.Modules.$Name.Contracts"
  $crel  = "src/Modules/$cproj"
  if (Test-Path (Join-Path $Root "$crel/$cproj.csproj")) { throw "$crel already exists." }
  New-Item -ItemType Directory -Force (Join-Path $Root $crel) | Out-Null
  $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Public surface of the $Name module: integration events and query interfaces only. -->
  <ItemGroup>
    <ProjectReference Include="..\..\Marketplace.SharedKernel\Marketplace.SharedKernel.csproj" />
  </ItemGroup>
</Project>
"@
  [IO.File]::WriteAllText((Join-Path $Root "$crel/$cproj.csproj"), ($csproj -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
  Push-Location $Root
  try {
    dotnet sln Marketplace.slnx add "$crel/$cproj.csproj" --solution-folder src/Modules | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'dotnet sln add failed.' }
    dotnet add "src/Modules/Marketplace.Modules.$Name" reference "$crel/$cproj.csproj" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'dotnet add reference failed.' }
  } finally { Pop-Location }
  Write-Output "Created $cproj. Reference it from consuming modules with: dotnet add src/Modules/Marketplace.Modules.<Consumer> reference $crel"
  return
}

$schema  = $Name.ToLowerInvariant()
$project = "Marketplace.Modules.$Name"
$dir     = Join-Path $Root "src/Modules/$project"
$catalog = Join-Path $Root 'src/Marketplace.Bootstrap/ModuleCatalog.cs'

if ((Test-Path $dir) -and -not $FilesOnly) { throw "$dir already exists." }

$utf8 = New-Object System.Text.UTF8Encoding($false)
function Save($path, $text) {
  New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
  [IO.File]::WriteAllText($path, ($text -replace "`r`n", "`n"), $utf8)
}

Save "$dir/$project.csproj" @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\Marketplace.SharedKernel\Marketplace.SharedKernel.csproj" />
  </ItemGroup>
</Project>
"@

$doc = [Security.SecurityElement]::Escape($Summary)
Save "$dir/${Name}Module.cs" @"
using Marketplace.Modules.$Name.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.$Name;

/// <summary>$doc</summary>
public sealed class ${Name}Module : IModule
{
    public const string Schema = "$schema";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<${Name}DbContext>(Schema);
}
"@

Save "$dir/Persistence/${Name}DbContext.cs" @"
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.$Name.Persistence;

/// <summary>
/// Code-first model of the "$schema" schema. Add DbSets here and IEntityTypeConfiguration classes
/// anywhere in this assembly, then: dotnet ef migrations add &lt;Name&gt; (see the skill's "Add a migration").
/// </summary>
internal sealed class ${Name}DbContext(DbContextOptions<${Name}DbContext> options) : ModuleDbContext(options)
{
    public override string Schema => ${Name}Module.Schema;
}
"@

Save "$dir/Persistence/${Name}DbContextFactory.cs" @"
using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.$Name.Persistence;

/// <summary>Lets dotnet ef build <see cref="${Name}DbContext"/> at design time.</summary>
internal sealed class ${Name}DbContextFactory : ModuleDesignTimeFactory<${Name}DbContext>
{
    protected override string Schema => ${Name}Module.Schema;
}
"@

if ($FilesOnly) { Write-Output "Rewrote source files of '$schema'."; return }

function Invoke-Checked([string]$what, [scriptblock]$cmd) {
  & $cmd | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE)." }
}

# dotnet ef needs repo-relative paths (absolute 8.3 temp paths break its metadata lookup).
$rel = "src/Modules/$project"

Push-Location $Root
try {
  Invoke-Checked 'dotnet sln add' { dotnet sln Marketplace.slnx add "$rel/$project.csproj" --solution-folder src/Modules }
  Invoke-Checked 'dotnet add reference' { dotnet add src/Marketplace.Bootstrap reference "$rel/$project.csproj" }

  # Register in ModuleCatalog.All: add the using and append before the closing "];".
  $text = [IO.File]::ReadAllText($catalog)
  $text = $text -replace '(using Marketplace\.SharedKernel;)', "using Marketplace.Modules.$Name;`n`$1"
  $text = $text -replace '(\r?\n)(\s*)\];', "`$1`$2    new ${Name}Module(),`$1`$2];"
  [IO.File]::WriteAllText($catalog, $text, $utf8)

  Invoke-Checked 'dotnet tool restore' { dotnet tool restore }
  # dotnet ef can't read a project that was never restored, so restore and compile the new module first.
  Invoke-Checked 'dotnet build' { dotnet build src/Marketplace.Api --nologo -v q }
  Invoke-Checked 'dotnet ef migrations add' {
    dotnet ef migrations add InitialCreate --project $rel --startup-project src/Marketplace.Api `
      --context "${Name}DbContext" --output-dir Persistence/Migrations
  }
} finally { Pop-Location }

Write-Output "Created module '$schema' at $dir, registered it in ModuleCatalog and added the InitialCreate migration."
Write-Output "Next: dotnet test Marketplace.slnx"
