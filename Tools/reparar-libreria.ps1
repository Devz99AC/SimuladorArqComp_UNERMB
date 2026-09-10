# =====================================================================
#  reparar-libreria.ps1   (segundo paso, tras reparar-shadergraph.ps1)
#  ---------------------------------------------------------------------
#  Reconstruye la base de datos de assets de Unity.
#
#  POR QUE HACE FALTA
#  Al restaurar com.unity.shadergraph, Unity comparo los archivos nuevos
#  con las huellas que tenia guardadas de la version corrupta y los marco
#  como "assets located in immutable packages were unexpectedly altered".
#  Con el paquete en ese estado NO compila sus ensamblados de editor:
#     - falta Unity.ShaderGraph.Editor.dll
#     - falta Unity.RenderPipelines.Universal.Editor.dll
#  y URP se queda sin los tipos de Shader Graph -> ~4400 errores CS0246
#  y el aviso "fallback shader 'Hidden/Shader Graph/FallbackError' not found".
#
#  QUE HACE
#  Borra todo Library\ EXCEPTO PackageCache (que ya se verifico sano, asi
#  se evita volver a descargar los paquetes). Unity reconstruye la base de
#  datos desde cero y vuelve a compilar todo en el orden correcto.
#
#  QUE NO TOCA
#  Assets\, ProjectSettings\, Packages\ ni nada versionado. Library\ no
#  tiene un solo archivo rastreado por git: es cache regenerable.
#
#  USO:  1. CERRAR Unity por completo.
#        2. Ejecutar este script.
#        3. Abrir el proyecto. La reimportacion completa tarda bastante
#           (modelos FBX pesados); dejalo terminar sin interrumpir.
# =====================================================================

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$library = Join-Path $project 'Library'

Write-Host ""
Write-Host "Reconstruccion de la base de datos de assets" -ForegroundColor Cyan
Write-Host "Proyecto: $project"
Write-Host ""

# --- 1. Unity debe estar cerrado ------------------------------------------
$unity = Get-Process Unity -ErrorAction SilentlyContinue
if ($unity) {
    Write-Host "ERROR: Unity sigue abierto (PID $($unity.Id -join ', '))." -ForegroundColor Red
    Write-Host "       Cierra el editor por completo y vuelve a ejecutar el script."
    exit 1
}

$lock = Join-Path $project 'Temp\UnityLockfile'
if (Test-Path $lock) {
    try   { $s = [System.IO.File]::Open($lock,'Open','ReadWrite','None'); $s.Close() }
    catch { Write-Host "ERROR: el proyecto sigue bloqueado por un editor abierto." -ForegroundColor Red; exit 1 }
}

if (-not (Test-Path $library)) {
    Write-Host "No existe Library\ : nada que hacer." -ForegroundColor DarkGray
    exit 0
}

# --- 2. Borrar Library\ salvo PackageCache --------------------------------
$kept = 'PackageCache'
$deleted = 0

Get-ChildItem -Path $library -Force | ForEach-Object {
    if ($_.Name -eq $kept) {
        Write-Host "Conservando Library\$kept (paquetes ya verificados sanos)" -ForegroundColor DarkGray
        return
    }
    Write-Host "Borrando Library\$($_.Name) ..." -ForegroundColor Yellow
    try {
        Remove-Item -Recurse -Force $_.FullName
        $deleted++
    } catch {
        Write-Host "   no se pudo borrar: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# --- 3. Temp\ tambien guarda estado intermedio del build ------------------
$temp = Join-Path $project 'Temp'
if (Test-Path $temp) {
    Write-Host "Borrando Temp\ ..." -ForegroundColor Yellow
    try { Remove-Item -Recurse -Force $temp } catch { }
}

Write-Host ""
Write-Host "Listo: $deleted elementos eliminados de Library\." -ForegroundColor Green
Write-Host ""
Write-Host "Abre el proyecto en Unity. Reimportara TODO el proyecto, asi que"
Write-Host "tardara varios minutos (los modelos FBX son pesados). Dejalo"
Write-Host "terminar sin interrumpir."
Write-Host ""
Write-Host "Al terminar, la consola no deberia mostrar ningun error CS0246"
Write-Host "ni el aviso de 'immutable packages were unexpectedly altered'."
Write-Host ""
