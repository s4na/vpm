#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Skinned Mesh RendererのBlendShapeをAnimationClipとして出力するエディタ拡張
/// VRChat向け - Editorフォルダに配置してください
/// </summary>
public static class BlendShapeToAnimation
{
    private const string MENU_PATH = "CONTEXT/SkinnedMeshRenderer/Export BlendShapes to Animation";

    [MenuItem(MENU_PATH, false, 1000)]
    private static void ExportBlendShapesToAnimation(MenuCommand command)
    {
        var smr = command.context as SkinnedMeshRenderer;
        if (smr == null || smr.sharedMesh == null)
        {
            Debug.LogError("[BlendShapeToAnimation] SkinnedMeshRendererまたはMeshが見つかりません");
            return;
        }

        var mesh = smr.sharedMesh;
        int blendShapeCount = mesh.blendShapeCount;

        if (blendShapeCount == 0)
        {
            EditorUtility.DisplayDialog("BlendShape Export", "このメッシュにはBlendShapeがありません", "OK");
            return;
        }

        // 0を含めるかどうか選択
        int option = EditorUtility.DisplayDialogComplex(
            "BlendShape Export",
            $"BlendShapeが {blendShapeCount} 個見つかりました。\n0の値も含めて保存しますか？",
            "0を含めない",  // 0
            "キャンセル",    // 1
            "0も含める"      // 2
        );

        if (option == 1) return;

        bool includeZero = (option == 2);

        // 保存先選択
        string defaultName = smr.gameObject.name + "_BlendShape";
        string savePath = EditorUtility.SaveFilePanelInProject(
            "Save Animation",
            defaultName,
            "anim",
            "アニメーションファイルの保存先を選択してください"
        );

        if (string.IsNullOrEmpty(savePath)) return;

        // 相対パス取得
        string relativePath = GetRelativePath(smr);

        // アニメーション作成
        var clip = new AnimationClip();
        clip.name = Path.GetFileNameWithoutExtension(savePath);

        int exportedCount = 0;

        for (int i = 0; i < blendShapeCount; i++)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);
            float value = smr.GetBlendShapeWeight(i);

            // 0を含めない場合はスキップ
            if (!includeZero && Mathf.Approximately(value, 0f)) continue;

            var curve = new AnimationCurve();
            curve.AddKey(0f, value);
            curve.AddKey(1f / 60f, value);

            string propertyName = $"blendShape.{blendShapeName}";
            clip.SetCurve(relativePath, typeof(SkinnedMeshRenderer), propertyName, curve);
            exportedCount++;
        }

        if (exportedCount == 0)
        {
            EditorUtility.DisplayDialog("BlendShape Export", "出力対象のBlendShapeがありませんでした", "OK");
            return;
        }

        // VRChat用設定
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Export Complete",
            $"{exportedCount} 個のBlendShapeを出力しました。\n\nPath: {relativePath}",
            "OK"
        );

        Selection.activeObject = clip;
        EditorGUIUtility.PingObject(clip);
    }

    private static string GetRelativePath(SkinnedMeshRenderer smr)
    {
        Transform root = smr.transform.root;
        Transform current = smr.transform;

        while (current.parent != null)
        {
            if (current.GetComponent<Animator>() != null)
            {
                root = current;
                break;
            }
            current = current.parent;
        }

        return AnimationUtility.CalculateTransformPath(smr.transform, root);
    }
}
#endif
