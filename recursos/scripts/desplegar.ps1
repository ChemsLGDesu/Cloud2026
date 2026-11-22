param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('development', 'production')]
    [string]$Ambiente,
    [switch]$Simular,
    [switch]$Confirmar,
    [switch]$VerificarEconomia,
    [string]$Revertir = "",
    [int]$EsperaSegundos = 45
)

$ErrorActionPreference = 'Continue'

if ($Ambiente -eq 'production' -and -not $Confirmar -and -not $Simular) {
    throw "Producción exige -Confirmar."
}

if ([string]::IsNullOrWhiteSpace($env:UGS_PROJECT_ID)) {
    throw "Falta la variable de entorno UGS_PROJECT_ID. Configúrala antes de desplegar."
}

$raiz = (Resolve-Path "$PSScriptRoot\..\..").Path
$proyecto = @('-p', $env:UGS_PROJECT_ID, '-e', $Ambiente)
$cronometroTotal = [System.Diagnostics.Stopwatch]::StartNew()
$tiempos = [ordered]@{}

function Medir-Fase([string]$nombre, [scriptblock]$accion) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $accion
    $sw.Stop()
    $tiempos[$nombre] = [math]::Round($sw.Elapsed.TotalSeconds, 1)
}

# Modo Reversión
if (-not [string]::IsNullOrWhiteSpace($Revertir)) {
    $rutaZip = if ([System.IO.Path]::IsPathRooted($Revertir)) { $Revertir } else { Join-Path $PSScriptRoot $Revertir }
    if (-not (Test-Path $rutaZip)) { throw "No existe el respaldo: $rutaZip" }
    Write-Host "-- Revertir módulo Tienda en $Ambiente desde $rutaZip" -ForegroundColor Yellow
    Medir-Fase "Reversión de módulo" {
        & ugs cloud-code modules import Tienda $rutaZip @proyecto
        if ($LASTEXITCODE -ne 0) { throw "Falló la importación del respaldo (.ccmzip)." }
    }
    Write-Host "Reversión completada en $($tiempos['Reversión de módulo']) s. Espera ~35 s para propagación." -ForegroundColor Green
    exit 0
}

$slnPath = Join-Path $raiz "CloudCode.Modules\Tienda\Tienda.sln"
if (-not (Test-Path $slnPath)) {
    throw "No se encontró la solución del módulo en: $slnPath"
}

# FASE 1: Pruebas locales
Medir-Fase "Pruebas del backend" {
    & dotnet test $slnPath --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "ABORTADO: Las pruebas unitarias fallaron. No se despliega." }
    Write-Host "-- Pruebas del backend           OK (superadas)" -ForegroundColor Green
}

# FASE 2: Respaldo del módulo publicado
Medir-Fase "Respaldo del módulo actual" {
    $carpetaRespaldos = Join-Path $raiz "respaldos"
    $antes = @(Get-ChildItem $carpetaRespaldos -Filter "*.ccmzip" -ErrorAction SilentlyContinue)
    if ($Simular) {
        Write-Host "-- [SIMULAR] Exportaría respaldo en respaldos\" -ForegroundColor Cyan
    } else {
        & ugs cloud-code modules export Tienda $carpetaRespaldos @proyecto 2>$null
        $despues = @(Get-ChildItem $carpetaRespaldos -Filter "*.ccmzip" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
        $nombreZip = if ($despues.Count -gt 0) { $despues[0].Name } else { "sin-modulo-previo.ccmzip" }
        Write-Host "-- Respaldo del módulo actual    $nombreZip"
    }
}

# FASE 3: Configuración remota y leaderboards (ANTES que el módulo)
$archivosConfig = @(
    "$raiz\plantillas\S05-economia.rc",
    "$raiz\plantillas\S06-tienda.rc",
    "$raiz\plantillas\S08-combate.rc",
    "$raiz\plantillas\S10-rivales.rc",
    "$raiz\plantillas\S11-temporada.rc",
    "$raiz\plantillas\S12-eventos.rc",
    "$raiz\plantillas\trofeos_dvsc.lb",
    "$raiz\plantillas\S11-politica-ranking.ac"
) | Where-Object { Test-Path $_ }

Medir-Fase "Configuración y leaderboards" {
    if ($Simular) {
        Write-Host "-- [SIMULAR] Desplegaría $($archivosConfig.Count) archivos (.rc, .lb, .ac) en $Ambiente" -ForegroundColor Cyan
    } else {
        & ugs deploy @archivosConfig @proyecto
        if ($LASTEXITCODE -ne 0) { throw "Error desplegando configuración remota (.rc / .lb / .ac)." }
        Write-Host "-- Configuración y leaderboards  6 .rc + .lb + .ac: Updated"
    }
}

# FASE 4: Compilación y despliegue del módulo Cloud Code
Medir-Fase "Módulo de Cloud Code (Tienda)" {
    if ($Simular) {
        Write-Host "-- [SIMULAR] Compilaría y desplegaría $slnPath en $Ambiente" -ForegroundColor Cyan
    } else {
        & dotnet build $slnPath -c Release --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Error compilando Tienda.sln." }
        & ugs deploy $slnPath @proyecto
        if ($LASTEXITCODE -ne 0) { throw "Error desplegando módulo Tienda." }
        Write-Host "-- Módulo de Cloud Code (Tienda) Updated"
    }
}

if ($Simular) {
    $cronometroTotal.Stop()
    Write-Host "`nSimulación (-Simular) finalizada en $([math]::Round($cronometroTotal.Elapsed.TotalSeconds, 1)) s sin modificar la nube." -ForegroundColor Green
    exit 0
}

# FASE 5: Espera de propagación + Verificación en vivo
Medir-Fase "Espera de propagación" {
    Write-Host "-- Espera de propagación ($EsperaSegundos s)"
    Start-Sleep -Seconds $EsperaSegundos
}

Medir-Fase "Verificación de versión" {
    $authResp = Invoke-RestMethod -Method Post `
        -Uri "https://player-auth.services.api.unity.com/v1/authentication/anonymous" `
        -Headers @{ "ProjectId" = $env:UGS_PROJECT_ID; "UnityEnvironment" = $Ambiente }
    $token = $authResp.sessionToken

    $verResp = Invoke-RestMethod -Method Post `
        -Uri "https://cloud-code.services.api.unity.com/v1/projects/$($env:UGS_PROJECT_ID)/modules/Tienda/HoraDelServidor" `
        -Headers @{
            "Authorization" = "Bearer $token"
            "UnityEnvironment" = $Ambiente
            "Content-Type" = "application/json"
        } -Body "{}"

    $out = $verResp.output
    Write-Host "-- Verificación                  entorno $($out.entorno) · kit $($out.kit)"
    if ($out.entorno -ne $Ambiente -or $out.kit -ne "dvsc-20261105") {
        throw "Verificación fallida: esperado ($Ambiente · dvsc-20261105), recibido ($($out.entorno) · $($out.kit))"
    }
}

if ($VerificarEconomia) {
    if ($Ambiente -eq 'production') {
        throw "-VerificarEconomia solo se permite en development porque crea jugadores de prueba."
    }
    Medir-Fase "Verificación económica" {
        $scriptPy = Join-Path $raiz "pruebas\verificar_backend.py"
        & python $scriptPy --project-id $env:UGS_PROJECT_ID --environment $Ambiente
        if ($LASTEXITCODE -ne 0) { throw "Falló la verificación económica (verificar_backend.py)." }
    }
}

$cronometroTotal.Stop()
Write-Host ""
foreach ($k in $tiempos.Keys) {
    Write-Host ("{0,-32} {1,5:N1} s" -f $k, $tiempos[$k])
}
Write-Host ("{0,-32} {1,5:N1} s" -f "TOTAL", [math]::Round($cronometroTotal.Elapsed.TotalSeconds, 1)) -ForegroundColor Green
