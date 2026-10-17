using UnityEditor;
using UnityEngine;

public static class FixTextureImporters
{
    [MenuItem("Tools/Quoc/MiniGame/Fix Switch Texture Importers")]
    public static void Fix()
    {
        FixTexture("Assets/_Project/Quoc/Asset/Obj/LavaCavePressureSwitch_Off.png");
        FixTexture("Assets/_Project/Quoc/Asset/Obj/LavaCavePressureSwitch_On.png");
        AssetDatabase.Refresh();
        Debug.Log("Switches Textures have been converted to Sprite (2D and UI)!");
    }

    private static void FixTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
