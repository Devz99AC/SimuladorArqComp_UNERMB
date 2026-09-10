# =====================================================================
#  reparar-shadergraph.ps1
#  ---------------------------------------------------------------------
#  Repara el paquete com.unity.shadergraph, cuyo cache local se corrompio:
#  430 de sus archivos (incluidos TODOS los .hlsl de ShaderGraphLibrary)
#  quedaron rellenos de espacios en blanco.
#
#  Sintomas que produce:
#    - "The .meta file ... does not have a valid GUID" (decenas de errores)
#    - "Host type is not matching any asset type at Path
#       Packages/com.unity.render-pipelines.universal/Shaders/Nature/
#       SpeedTree8_PBRLit.shadergraph"
#    - "Shader error in 'Shader Graphs/...': undeclared identifier
#       'DecodeHDRSample'" en TODOS los Shader Graph del proyecto
#      (Attenuation2, alpha, unlitatt, Leaves, Spec+RimColorManual...),
#      lo que deja en magenta los materiales de la mesa, la placa, la CPU,
#      la GPU y el disco duro.
#
#  Que hace: borra la copia corrupta del cache para que Unity la vuelva a
#  descargar limpia del registro al abrir el proyecto. No toca nada de
#  Assets/ ni de ProjectSettings/.
#
#  USO:  1. CERRAR Unity por completo.
#        2. Ejecutar este script.
#        3. Abrir el proyecto. Unity descargara el paquete de nuevo
#           (tarda unos minutos) y recompilara los shaders.
# =====================================================================

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot

Write-Host ""
Write-Host "Reparacion del cache de com.unity.shadergraph" -ForegroundColor Cyan
Write-Host "Proyecto: $project"
Write-Host ""

# --- 1. Comprobar que Unity no esta abierto -------------------------------
$unity = Get-Process Unity -ErrorAction SilentlyContinue
if ($unity) {
    Write-Host "ERROR: Unity sigue abierto (PID $($unity.Id -join ', '))." -ForegroundColor Red
    Write-Host "       Cierra el editor por completo y vuelve a ejecutar el script."
    exit 1
}

# El lock del proyecto tambien delata a un editor abierto o mal cerrado.
$lock = Join-Path $project 'Temp\UnityLockfile'
if (Test-Path $lock) {
    try {
        $stream = [System.IO.File]::Open($lock, 'Open', 'ReadWrite', 'None')
        $stream.Close()
    } catch {
        Write-Host "ERROR: el proyecto sigue bloqueado por un editor abierto." -ForegroundColor Red
        exit 1
    }
}

# --- 2. Borrar la copia corrupta del PackageCache -------------------------
$removed = 0
Get-ChildItem -Path (Join-Path $project 'Library\PackageCache') -Directory `
              -Filter 'com.unity.shadergraph@*' -ErrorAction SilentlyContinue |
    ForEach-Object {
        Write-Host "Borrando $($_.Name) ..." -ForegroundColor Yellow
        Remove-Item -Recurse -Force $_.FullName
        $removed++
    }

if ($removed -eq 0) {
    Write-Host "No habia copia en Library\PackageCache (ya estaba limpia)." -ForegroundColor DarkGray
}

# --- 3. Borrar tambien la copia del cache global de UPM -------------------
# Si el tarball global tambien esta danado, Unity volveria a extraer basura.
foreach ($base in @(
    (Join-Path $env:LOCALAPPDATA 'Unity\cache\packages\packages.unity.com'),
    (Join-Path $env:LOCALAPPDATA 'Unity\cache\npm\packages.unity.com')
)) {
    if (-not (Test-Path $base)) { continue }
    Get-ChildItem -Path $base -Filter 'com.unity.shadergraph*' -ErrorAction SilentlyContinue |
        ForEach-Object {
            Write-Host "Borrando cache global: $($_.FullName)" -ForegroundColor Yellow
            Remove-Item -Recurse -Force $_.FullName
        }
}

# --- 4. Invalidar los shaders compilados ---------------------------------
# Fuerza a Unity a recompilar los Shader Graph con la libreria ya sana.
$shaderCache = Join-Path $project 'Library\ShaderCache'
if (Test-Path $shaderCache) {
    Write-Host "Borrando Library\ShaderCache ..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $shaderCache
}

Write-Host ""
Write-Host "Listo. Abre el proyecto en Unity." -ForegroundColor Green
Write-Host "La primera apertura tardara mas: se descarga el paquete y se"
Write-Host "recompilan los shaders. Comprueba luego que la consola ya no"
Write-Host "muestra errores de 'DecodeHDRSample' ni de '.meta ... valid GUID'."
Write-Host ""
