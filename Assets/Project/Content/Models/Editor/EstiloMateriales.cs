using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Pasa los materiales incrustados de los FBX al sombreado estilizado del proyecto (Attenuation2),
// el mismo de la mesa y las piezas viejas. Ese shader solo lee una textura de color (_Textura):
//   - material con textura y sin tinte  → usa esa textura tal cual;
//   - material con textura y con tinte  → copia teñida de la textura;
//   - material sin textura              → textura de color liso generada (compartida por color).
// Los .mat nuevos quedan en <carpeta del FBX>/Materiales y el FBX los usa por remapeo, así que
// reimportar el modelo no los pisa. Volver a ejecutar rehace la conversión desde el FBX.
public static class EstiloMateriales
{
    const string ShaderPath = "Assets/3D/ShaderAdditionalLight/Attenuation2.shadergraph";
    const int MaxTinteSize = 1024;

    struct Ajuste
    {
        public float brillo, rango, rim, tamRim, specular, rangoSpecular, tamSpecular;
    }

    // Valores tomados de materiales que ya existen en el proyecto (metalrings, cpu, gpu).
    static readonly Ajuste Metal = new Ajuste { brillo = 1f, rango = 0.91f, rim = 0.35f, tamRim = 0.6f, specular = 0.45f, rangoSpecular = 200f, tamSpecular = 0.9f };
    static readonly Ajuste Texturizado = new Ajuste { brillo = 1f, rango = 0.95f, rim = 0.12f, tamRim = 1.3f, specular = 0.1f, rangoSpecular = 4.3f, tamSpecular = 0.1f };
    static readonly Ajuste Liso = new Ajuste { brillo = 1f, rango = 0.95f, rim = 0.15f, tamRim = 0.9f, specular = 0.12f, rangoSpecular = 4.3f, tamSpecular = 0.3f };

    [MenuItem("Tools/Simulador/Materiales estilizados (modelos seleccionados)")]
    static void ConvertSelection()
    {
        var paths = new List<string>();
        foreach (var guid in Selection.assetGUIDs)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(p)) paths.AddRange(FindModels(p));
            else if (AssetImporter.GetAtPath(p) is ModelImporter) paths.Add(p);
        }
        if (paths.Count == 0)
        {
            Debug.LogWarning("Selecciona uno o más FBX (o la carpeta que los contiene).");
            return;
        }
        Debug.Log(Convert(paths));
    }

    public static string ConvertFolder(string folder) => Convert(FindModels(folder));

    static List<string> FindModels(string folder)
    {
        var list = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(p) is ModelImporter) list.Add(p);
        }
        return list;
    }

    public static string Convert(List<string> modelPaths)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) return "No se encontró el shader " + ShaderPath;

        var log = new StringBuilder();
        foreach (string modelPath in modelPaths)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);

            // Quitar remapeos anteriores para leer los materiales originales del FBX.
            var oldMap = importer.GetExternalObjectMap();
            bool hadRemaps = false;
            foreach (var kv in oldMap)
                if (kv.Key.type == typeof(Material)) { importer.RemoveRemap(kv.Key); hadRemaps = true; }
            if (hadRemaps) importer.SaveAndReimport();

            string modelDir = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            string matDir = modelDir + "/Materiales";
            string colorDir = Path.GetDirectoryName(modelDir).Replace('\\', '/') + "/Colores";
            EnsureFolder(matDir);

            log.AppendLine(Path.GetFileName(modelPath) + ":");
            var usedNames = new HashSet<string>(); // Windows no distingue "Material" de "material"
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (!(obj is Material src)) continue;
                string fileName = Safe(src.name);
                for (int k = 2; !usedNames.Add(fileName.ToLowerInvariant()); k++) fileName = Safe(src.name) + "_" + k;

                Texture2D tex = null;
                if (src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap") as Texture2D;
                if (tex == null) tex = src.mainTexture as Texture2D;
                Color col = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.color;
                bool metal = IsMetal(src);

                Texture2D target;
                string kind;
                if (tex != null && IsWhite(col)) { target = tex; kind = "textura"; }
                else if (tex != null) { target = TintedCopy(tex, col, matDir + "/" + fileName + "_tinte.png"); kind = "textura teñida"; }
                else
                {
                    col = Paleta(col);
                    target = SolidColor(col, colorDir);
                    kind = "color " + ColorUtility.ToHtmlStringRGB(col);
                }

                string matPath = matDir + "/" + fileName + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader) { name = fileName };
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else mat.shader = shader;

                Ajuste a = metal ? Metal : tex != null ? Texturizado : Liso;
                mat.SetTexture("_Textura", target);
                mat.SetFloat("_Brillo", a.brillo);
                mat.SetFloat("_Rango", a.rango);
                mat.SetFloat("_Intensidad_Rim", a.rim);
                mat.SetFloat("_Tama_o_Rim", a.tamRim);
                mat.SetFloat("_Intensidad_Specular", a.specular);
                mat.SetFloat("_Rango_Specular", a.rangoSpecular);
                mat.SetFloat("_Tama_o_Specular", a.tamSpecular);
                EditorUtility.SetDirty(mat);

                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
                log.AppendLine("  " + src.name + " → " + kind + (metal ? " (metal)" : ""));
            }
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();
        }
        return log.ToString();
    }

    /// <summary>Material estilizado de color liso (para piezas añadidas por código: tapas, separadores…).</summary>
    public static Material SolidMaterial(string matPath, string colorDir, Color color, bool metal)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        EnsureFolder(Path.GetDirectoryName(matPath).Replace('\\', '/'));
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(matPath) };
            AssetDatabase.CreateAsset(mat, matPath);
        }
        Ajuste a = metal ? Metal : Liso;
        mat.SetTexture("_Textura", SolidColor(Paleta(color), colorDir));
        mat.SetFloat("_Brillo", a.brillo);
        mat.SetFloat("_Rango", a.rango);
        mat.SetFloat("_Intensidad_Rim", a.rim);
        mat.SetFloat("_Tama_o_Rim", a.tamRim);
        mat.SetFloat("_Intensidad_Specular", a.specular);
        mat.SetFloat("_Rango_Specular", a.rangoSpecular);
        mat.SetFloat("_Tama_o_Specular", a.tamSpecular);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static bool IsWhite(Color c) => c.r > 0.97f && c.g > 0.97f && c.b > 0.97f;

    // El FBX no conserva el metalizado del original, así que se deduce del nombre del material.
    static readonly string[] MetalWords = { "metal", "mertal", "chrome", "pins", "lid" };

    static bool IsMetal(Material m)
    {
        if (m.HasProperty("_Metallic") && m.GetFloat("_Metallic") >= 0.5f) return true;
        string n = m.name.ToLowerInvariant();
        foreach (string w in MetalWords)
            if (n.Contains(w)) return true;
        return false;
    }

    // Misma gama que la paleta de las piezas viejas (negro #1B1B1B, blanco #E9E9E9): con el
    // sombreado estilizado, un negro puro se lee como un agujero y un blanco puro se quema.
    const float PaletaMin = 27f / 255f, PaletaMax = 233f / 255f;

    static Color Paleta(Color c) => new Color(
        Mathf.Lerp(PaletaMin, PaletaMax, c.r),
        Mathf.Lerp(PaletaMin, PaletaMax, c.g),
        Mathf.Lerp(PaletaMin, PaletaMax, c.b), 1f);

    static string Safe(string name)
    {
        foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
        return name.Replace(':', '_').Trim();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Texture2D SolidColor(Color c, string dir)
    {
        EnsureFolder(dir);
        string path = dir + "/Color_" + ColorUtility.ToHtmlStringRGB(c) + ".png";
        if (!File.Exists(path))
        {
            var t = new Texture2D(4, 4, TextureFormat.RGB24, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(c.r, c.g, c.b, 1f);
            t.SetPixels(px);
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Lee la textura vía GPU (no hace falta marcarla legible) y multiplica por el tinte.
    static Texture2D TintedCopy(Texture2D src, Color tint, string path)
    {
        int w = src.width, h = src.height;
        while (w > MaxTinteSize || h > MaxTinteSize) { w /= 2; h /= 2; }

        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var t = new Texture2D(w, h, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = t.GetPixels();
        for (int i = 0; i < px.Length; i++) px[i] = new Color(px[i].r * tint.r, px[i].g * tint.g, px[i].b * tint.b, 1f);
        t.SetPixels(px);
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
