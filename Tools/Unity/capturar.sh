#!/bin/bash
# Captura vistas de la escena abierta en el editor (modo edición o Play) sin crear assets.
# Usa una cámara temporal que copia la Main Camera y deja PNG en $OUT (por defecto Temp/capturas,
# que git ignora y Unity borra al cerrarse). Necesita el editor abierto con com.unity.pipeline.
#
#   Tools/Unity/capturar.sh nombre "px py pz  lx ly lz  [fov]  [ortho]" ...
#     px py pz = posición de la cámara · lx ly lz = punto al que mira · fov (40 por defecto)
#     ortho > 0 = cámara ortográfica con ese medio alto (en metros); útil para medir sobre la imagen.
#   Variables: W, H (resolución, 1280x720), SD (distancia de sombras durante la captura, 4 m), OUT.
#
# Truco para vistas cenitales: pon la cámara 0,0001 m por DELANTE (z menor) del punto que mira; así la
# parte de arriba de la imagen es +Z y la derecha +X (si va detrás, la imagen sale girada 180°).
# Ver CLAUDE.md §9 (CLI) y Docs/Plan_Laboratorios_Practicos.md.
PROJECT="C:\\Users\\devin\\SimuladorArqComp_UNERMB"
cd "$(dirname "$0")/../.." || exit 1
OUT=${OUT:-$PWD/Temp/capturas}
mkdir -p "$OUT"
WOUT=$(cygpath -m "$OUT" 2>/dev/null || echo "$OUT")
name=$1; shift
{
cat <<CS
var main=UnityEngine.Camera.main;
var urp=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset; float sdPrev=urp.shadowDistance; urp.shadowDistance=${SD:-4}f;
var go=new UnityEngine.GameObject("__cap"); go.hideFlags=UnityEngine.HideFlags.HideAndDontSave;
var cam=go.AddComponent<UnityEngine.Camera>(); cam.CopyFrom(main); cam.enabled=false;
var ad=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
ad.renderPostProcessing=true; ad.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
int W=${W:-1280},H=${H:-720};
var rt=new UnityEngine.RenderTexture(W,H,24,UnityEngine.RenderTextureFormat.ARGB32); rt.antiAliasing=4;
cam.targetTexture=rt;
var tex=new UnityEngine.Texture2D(W,H,UnityEngine.TextureFormat.RGB24,false);
var outs=new System.Text.StringBuilder();
System.Action<float,float,float,float,float,float,float,float,string> Shot=(px,py,pz,lx,ly,lz,fov,ortho,f)=>{
 cam.transform.position=new UnityEngine.Vector3(px,py,pz); cam.transform.LookAt(new UnityEngine.Vector3(lx,ly,lz));
 cam.orthographic=ortho>0; if(ortho>0) cam.orthographicSize=ortho; cam.fieldOfView=fov;
 cam.Render(); var prev=UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active=rt;
 tex.ReadPixels(new UnityEngine.Rect(0,0,W,H),0,0); tex.Apply(); UnityEngine.RenderTexture.active=prev;
 System.IO.File.WriteAllBytes(f,tex.EncodeToPNG()); outs.AppendLine(f);
};
CS
i=0
for v in "$@"; do
  set -- $v
  echo "Shot(${1}f,${2}f,${3}f,${4}f,${5}f,${6}f,${7:-40}f,${8:-0}f,\"$WOUT/${name}_${i}.png\");"
  i=$((i+1))
done
cat <<CS
cam.targetTexture=null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
urp.shadowDistance=sdPrev;
return outs.ToString();
CS
} > Temp/claude_cap.cs
unity command eval_file --file "Temp/claude_cap.cs" --result-only --timeout 60 --project-path "$PROJECT" --caller plugin --skill unity-cli --no-banner
