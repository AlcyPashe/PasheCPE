<#
.SYNOPSIS
    Verifica que PasheCPE y PasheCPE.Pdf se restauran y funcionan desde un proyecto de consola limpio.
.DESCRIPTION
    Crea un proyecto fuera del repositorio (sin Directory.Build.props ni Directory.Packages.props), con su propio
    nuget.config y una caché de paquetes aislada, agrega los dos paquetes con la versión indicada y ejecuta la prueba
    de humo de tools/verificar-paquete/Program.cs.txt: genera y firma una factura y su PDF, sin llamar a SUNAT.
.PARAMETER Version
    Versión exacta de los paquetes, por ejemplo 1.0.0-rc.1.
.PARAMETER Origen
    Local: la carpeta artifacts del repositorio (después de dotnet pack -p:Version=...).
    GitHub: GitHub Packages con el PAT read:packages de la variable de entorno PASHE_NUGET_PAT.
.EXAMPLE
    pwsh tools/verificar-paquete.ps1 -Version 1.0.0-rc.1 -Origen Local
#>
param(
    [Parameter(Mandatory)][string] $Version,
    [ValidateSet('Local', 'GitHub')][string] $Origen = 'Local'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$trabajo = Join-Path ([IO.Path]::GetTempPath()) "pashecpe-verificar-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$proyecto = Join-Path $trabajo 'Consumidor'
$cache = Join-Path $trabajo 'nuget-packages'

if ($Origen -eq 'Local') {
    $artifacts = Join-Path $repo 'artifacts'
    foreach ($id in 'PasheCPE', 'PasheCPE.Pdf') {
        if (-not (Test-Path (Join-Path $artifacts "$id.$Version.nupkg"))) { throw "No existe artifacts/$id.$Version.nupkg: ejecute dotnet pack PasheCPE.slnx -c Release -p:Version=$Version -o artifacts" }
    }
    $fuente = "<add key=`"pashe`" value=`"$artifacts`" />"
    $credenciales = ''
}
else {
    if (-not $env:PASHE_NUGET_PAT) { throw 'Falta la variable de entorno PASHE_NUGET_PAT (token de GitHub con read:packages).' }
    $fuente = '<add key="pashe" value="https://nuget.pkg.github.com/AlcyPashe/index.json" />'
    # El token se resuelve desde la variable de entorno: no se escribe en el archivo ni en la consola.
    $credenciales = @'
  <packageSourceCredentials>
    <pashe>
      <add key="Username" value="AlcyPashe" />
      <add key="ClearTextPassword" value="%PASHE_NUGET_PAT%" />
    </pashe>
  </packageSourceCredentials>
'@
}

$cacheAnterior = $env:NUGET_PACKAGES
try {
    New-Item -ItemType Directory -Force $proyecto | Out-Null
    # Caché propia: un paquete restaurado antes desde otro origen no da por buena la prueba.
    $env:NUGET_PACKAGES = $cache

    Set-Content -Encoding utf8NoBOM (Join-Path $trabajo 'nuget.config') @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    $fuente
  </packageSources>
$credenciales</configuration>
"@

    Write-Host "Proyecto limpio en $proyecto (origen $Origen, versión $Version)"
    dotnet new console --framework net10.0 --output $proyecto --name Consumidor | Out-Null
    if ($LASTEXITCODE) { throw 'dotnet new console falló' }
    foreach ($id in 'PasheCPE', 'PasheCPE.Pdf') {
        dotnet add $proyecto package $id --version $Version
        if ($LASTEXITCODE) { throw "No se pudo restaurar $id $Version desde $Origen" }
    }

    Copy-Item (Join-Path $PSScriptRoot 'verificar-paquete/Program.cs.txt') (Join-Path $proyecto 'Program.cs') -Force
    dotnet run --project $proyecto -- (Join-Path $trabajo 'CPE')
    if ($LASTEXITCODE) { throw 'La prueba de humo falló' }

    Write-Host "Verificación correcta: PasheCPE $Version desde $Origen" -ForegroundColor Green
}
finally {
    $env:NUGET_PACKAGES = $cacheAnterior
    Remove-Item -Recurse -Force $trabajo -ErrorAction SilentlyContinue
}
