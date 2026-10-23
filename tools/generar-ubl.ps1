#Requires -Version 7.0
<#
.SYNOPSIS
    Genera la referencia UBL (clases C# completas) desde los XSD oficiales de SUNAT.

.DESCRIPTION
    Ejecuta dotnet-xscgen (herramienta local, .config/dotnet-tools.json) sobre los XSD de recursos/XSD y deja el
    resultado en tools/ubl-referencia/. La referencia no compila ni se versiona (.gitignore): sirve para copiar
    nombres, namespaces y orden de los elementos al perfil SUNAT de src/PasheCPE/Ubl (TRD §3.1).
    Requiere el runtime de .NET 8 (dotnet-xscgen apunta a net8.0).

.EXAMPLE
    pwsh tools/generar-ubl.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$salida = Join-Path $raiz 'tools/ubl-referencia'

$documentos21 = 'Invoice', 'CreditNote', 'DebitNote', 'DespatchAdvice', 'ApplicationResponse' |
    ForEach-Object { Join-Path $raiz "recursos/XSD/2.1/maindoc/UBL-$_-2.1.xsd" }
$documentos20 = 'SummaryDocuments', 'VoidedDocuments' |
    ForEach-Object { Join-Path $raiz "recursos/XSD/2.0/maindoc/UBLPE-$_-1.0.xsd" }

$espacios = @(
    'urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2=Referencia.Cac'
    'urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2=Referencia.Cbc'
    'urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2=Referencia.Ext'
    'urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1=Referencia.Sac'
    'http://www.w3.org/2000/09/xmldsig#=Referencia.Ds'
)

Push-Location $raiz
try {
    dotnet tool restore | Out-Null
    if (Test-Path $salida) { Remove-Item $salida -Recurse -Force }

    $argumentos21 = @('--output', (Join-Path $salida '2.1'), '--namespace', 'Referencia.Documentos') +
        ($espacios | ForEach-Object { @('--namespace', $_) }) + $documentos21
    dotnet xscgen @argumentos21
    if ($LASTEXITCODE -ne 0) { throw "xscgen falló con los XSD 2.1 (código $LASTEXITCODE)." }

    $argumentos20 = @('--output', (Join-Path $salida '2.0'), '--namespace', 'Referencia.Documentos') +
        ($espacios | ForEach-Object { @('--namespace', $_) }) + $documentos20
    dotnet xscgen @argumentos20
    if ($LASTEXITCODE -ne 0) { throw "xscgen falló con los XSD 2.0 (código $LASTEXITCODE)." }

    $archivos = (Get-ChildItem $salida -Recurse -Filter *.cs).Count
    Write-Host "Referencia UBL generada en tools/ubl-referencia ($archivos archivos)."
}
finally {
    Pop-Location
}
