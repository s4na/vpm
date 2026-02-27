#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// AnimationClipのBlendShapeウェイトをSkinnedMeshRendererにインポートするエディタ拡張
/// VRChat向け - Editorフォルダに配置してください
/// </summary>
public class BlendShapeFromAnimation : EditorWindow
{
    private SkinnedMeshRenderer targetRenderer;
    private AnimationClip sourceClip;

    private const string MENU_PATH = "CONTEXT/SkinnedMeshRenderer/Import BlendShapes from Animation";

    [MenuItem(MENU_PATH, false, 1001)]
    private static void OpenFromContext(MenuCommand command)
    {
        var smr = command.context as SkinnedMeshRenderer;
        var window = GetWindow<BlendShapeFromAnimation>("Import BlendShapes");
        window.minSize = new Vector2(400, 150);
        window.maxSize = new Vector2(800, 150);

        if (smr != null)
        {
            window.targetRenderer = smr;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        // 対象 SkinnedMeshRenderer
        targetRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
            "対象 SMR", targetRenderer, typeof(SkinnedMeshRenderer), true);

        EditorGUILayout.Space(5);

        // インポート元 AnimationClip
        sourceClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Animation Clip", sourceClip, typeof(AnimationClip), false);

        EditorGUILayout.Space(10);

        // インポートボタン（両方選択済みの場合のみ有効）
        EditorGUI.BeginDisabledGroup(targetRenderer == null || sourceClip == null);
        if (GUILayout.Button("インポート", GUILayout.Height(30)))
        {
            ImportBlendShapes();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(5);

        // ガイドメッセージ
        if (targetRenderer == null)
        {
            EditorGUILayout.HelpBox("SkinnedMeshRendererを指定してください", MessageType.Info);
        }
        else if (sourceClip == null)
        {
            EditorGUILayout.HelpBox("インポート元の .anim ファイルを選択してください", MessageType.Info);
        }
    }

    private void ImportBlendShapes()
    {
        if (targetRenderer == null || targetRenderer.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Import Error", "SkinnedMeshRendererまたはMeshが見つかりません", "OK");
            return;
        }

        var mesh = targetRenderer.sharedMesh;
        var bindings = AnimationUtility.GetCurveBindings(sourceClip);

        int importedCount = 0;
        int skippedCount = 0;

        Undo.RecordObject(targetRenderer, "Import BlendShapes from Animation");

        foreach (var binding in bindings)
        {
            // BlendShapeのカーブだけを対象にする
            if (!binding.propertyName.StartsWith("blendShape.")) continue;

            string shapeName = binding.propertyName.Substring("blendShape.".Length);
            int shapeIndex = mesh.GetBlendShapeIndex(shapeName);

            // メッシュに存在しないBlendShapeはスキップ
            if (shapeIndex < 0)
            {
                skippedCount++;
                continue;
            }

            // カーブの時刻0の値を取得してウェイトに適用
            var curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
            float value = curve.Evaluate(0f);
            targetRenderer.SetBlendShapeWeight(shapeIndex, value);
            importedCount++;
        }

        EditorUtility.SetDirty(targetRenderer);

        if (importedCount == 0 && skippedCount == 0)
        {
            EditorUtility.DisplayDialog("Import", "このAnimationClipにBlendShapeのデータがありませんでした", "OK");
            return;
        }

        string message = $"{importedCount} 個のBlendShapeをインポートしました。";
        if (skippedCount > 0)
        {
            message += $"\n{skippedCount} 個はメッシュに存在しないためスキップしました。";
        }

        EditorUtility.DisplayDialog("Import Complete", message, "OK");
        Close();
    }
}
#endif
