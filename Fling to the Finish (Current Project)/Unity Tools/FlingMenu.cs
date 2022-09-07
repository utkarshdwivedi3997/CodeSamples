#if UNITY_EDITOR
using Fling.Saves;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class FlingMenu
{
    [MenuItem("FlingMenu/Pring all stars unlocked")]
    private static void PrintAllStarsUnlocked()
    {
        Debug.Log(SaveManager.Instance.GetTotalStarsUnlocked());
    }

    [MenuItem("FlingMenu/Pring all stars unlocked in autumn world")]
    private static void PrintAllStarsUnlockedInAutumn()
    {
        Debug.Log(SaveManager.Instance.GetTotalStarsInWorld(MetaManager.Instance.AllWorldsDictionary[Fling.Levels.WorldType.AUTUMN]));
    }

    [MenuItem("FlingMenu/Pring all stars unlocked in autumn park")]
    private static void PrintAllStarsUnlockedInAutumnPark()
    {
        Debug.Log(SaveManager.Instance.GetTotalStarsInLevel(MetaManager.Instance.AllWorldsDictionary[Fling.Levels.WorldType.AUTUMN].Levels[0]));
    }

    [MenuItem("FlingMenu/Pring all stars unlocked in autumn park coin frenzy")]
    private static void PrintAllStarsUnlockedInAutumnParkInCoinFrenzy()
    {
        Debug.Log(SaveManager.Instance.GetTotalStarsInModeInLevel(MetaManager.Instance.AllWorldsDictionary[Fling.Levels.WorldType.AUTUMN].Levels[0], Fling.GameModes.GameMode.CoinFrenzy));
    }

    [MenuItem("FlingMenu/Print the count of selected gameobjects")]
    private static void PrintNumberOfSelectedGameObjects()
    {
        Debug.Log(Selection.gameObjects.Length);
    }

    [MenuItem("FlingMenu/Generate Banner Background Scriptable Objects")]
    public static void CreateBannerBannerScriptableObjects()
    {
        string[] files = Directory.GetFiles("Assets/_Art/UI/AvatarBannerSticker/BannerSpriteAtlases", "*.png", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(file).OfType<Sprite>().ToArray();

            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                ScriptableObject obj = ScriptableObject.CreateInstance<BannerBackgroundScriptableObject>();
                AssetDatabase.CreateAsset(obj, "Assets/_Tech/ScriptableObjects/Banners/Backgrounds/Banner_" + sprite.name + ".asset");
                (obj as BannerBackgroundScriptableObject).Init(sprite);
                EditorUtility.SetDirty(obj);
            }
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("FlingMenu/Generate Banner Sticker Scriptable Objects")]
    public static void CreateBannerStickerScriptableObjects()
    {
        string[] files = Directory.GetFiles("Assets/_Art/UI/AvatarBannerSticker/StickerSpriteAtlases", "*.png", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(file).OfType<Sprite>().ToArray();

            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                ScriptableObject obj = ScriptableObject.CreateInstance<BannerStickerScriptableObject>();
                AssetDatabase.CreateAsset(obj, "Assets/_Tech/ScriptableObjects/Banners/Stickers/Sticker_" + sprite.name + ".asset");
                (obj as BannerStickerScriptableObject).Init(sprite);
                EditorUtility.SetDirty(obj);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
#endif