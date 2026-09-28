<#
    Trae los esquemas XSD de UBL 2.1 desde OASIS y los deja en schemas/ubl-2.1.

    Se ejecuta UNA vez: los esquemas quedan versionados en el repositorio para
    que la validacion sea reproducible y no dependa de que OASIS siga en pie.
    Este script queda como registro de su procedencia.
#>
$ErrorActionPreference = "Stop"

$url     = "https://docs.oasis-open.org/ubl/os-UBL-2.1/UBL-2.1.zip"
$destino = "schemas\ubl-2.1"
$zip     = Join-Path $env:TEMP "UBL-2.1.zip"
$tmp     = Join-Path $env:TEMP "ubl-extraccion"

Write-Host "Descargando UBL 2.1 desde OASIS (55 MB)..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $zip

Write-Host "Extrayendo..." -ForegroundColor Cyan
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $tmp -Force

$origen = Get-ChildItem -Path $tmp -Directory -Recurse -Filter "xsd" |
          Select-Object -First 1

if (-not $origen) { throw "No se encontro la carpeta xsd dentro del paquete." }

if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }
New-Item -ItemType Directory -Force -Path $destino | Out-Null

Copy-Item -Path (Join-Path $origen.FullName "*") -Destination $destino -Recurse -Force

Remove-Item $zip -Force
Remove-Item $tmp -Recurse -Force

$cuantos = (Get-ChildItem $destino -Recurse -Filter *.xsd).Count
$tamano  = "{0:N1}" -f ((Get-ChildItem $destino -Recurse | Measure-Object Length -Sum).Sum / 1MB)

Write-Host ""
Write-Host "Listo: $cuantos esquemas en $destino ($tamano MB)" -ForegroundColor Green
Write-Host "El documento principal de una factura es:" -ForegroundColor Gray
Write-Host "  $destino\maindoc\UBL-Invoice-2.1.xsd" -ForegroundColor Gray
