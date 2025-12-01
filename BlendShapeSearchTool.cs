using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class BlendShapeSearchTool : EditorWindow
{
    private SkinnedMeshRenderer targetRenderer;
    private string searchQuery = "";
    private Vector2 scrollPosition;
    private Dictionary<int, float> blendShapeValues = new Dictionary<int, float>();
    
    // 検索結果キャッシュ
    private List<(int index, string name)> filteredBlendShapes = new List<(int, string)>();
    private string lastQuery = null;
    private SkinnedMeshRenderer lastRenderer = null;

    [MenuItem("Tools/VRChat/BlendShape Search Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<BlendShapeSearchTool>("BlendShape Search");
        window.minSize = new Vector2(350, 400);
    }

    // SkinnedMeshRendererの3点リーダー（コンテキストメニュー）から開く
    [MenuItem("CONTEXT/SkinnedMeshRenderer/BlendShape Search Tool")]
    private static void OpenFromContext(MenuCommand command)
    {
        var renderer = command.context as SkinnedMeshRenderer;
        var window = GetWindow<BlendShapeSearchTool>("BlendShape Search");
        window.minSize = new Vector2(350, 400);
        
        if (renderer != null)
        {
            window.SetTarget(renderer);
        }
    }

    public void SetTarget(SkinnedMeshRenderer renderer)
    {
        targetRenderer = renderer;
        CacheBlendShapeValues();
        lastQuery = null;
        Repaint();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        OnSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        if (Selection.activeGameObject != null)
        {
            var renderer = Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>();
            if (renderer != null && renderer.sharedMesh != null)
            {
                targetRenderer = renderer;
                CacheBlendShapeValues();
                lastQuery = null; // 検索結果を更新
                Repaint();
            }
        }
    }

    private void CacheBlendShapeValues()
    {
        blendShapeValues.Clear();
        if (targetRenderer == null || targetRenderer.sharedMesh == null) return;

        int count = targetRenderer.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
        {
            blendShapeValues[i] = targetRenderer.GetBlendShapeWeight(i);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        
        // ヘッダー
        EditorGUILayout.LabelField("BlendShape Search Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        // ターゲット表示
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("Target", targetRenderer, typeof(SkinnedMeshRenderer), true);
        EditorGUI.EndDisabledGroup();

        if (targetRenderer == null || targetRenderer.sharedMesh == null)
        {
            EditorGUILayout.HelpBox("SkinnedMeshRendererを持つオブジェクトを選択してください", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(5);

        // 検索ボックス
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("🔍", GUILayout.Width(20));
        EditorGUI.BeginChangeCheck();
        searchQuery = EditorGUILayout.TextField(searchQuery);
        if (EditorGUI.EndChangeCheck())
        {
            lastQuery = null; // 検索更新フラグ
        }
        if (GUILayout.Button("✕", GUILayout.Width(25)))
        {
            searchQuery = "";
            lastQuery = null;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        // 検索ヘルプ
        EditorGUILayout.HelpBox(
            "検索例:\n" +
            "・eye mouth → 「eye」または「mouth」を含む (OR検索)\n" +
            "・eye -blink → 「eye」を含み「blink」を含まない\n" +
            "・vrc -left -right → 「vrc」のみ（leftとrightを除外）",
            MessageType.None);

        EditorGUILayout.Space(5);

        // 検索実行
        UpdateFilteredBlendShapes();

        // 結果カウント
        int totalCount = targetRenderer.sharedMesh.blendShapeCount;
        EditorGUILayout.LabelField($"表示: {filteredBlendShapes.Count} / {totalCount}", EditorStyles.miniLabel);

        EditorGUILayout.Space(3);

        // 一括操作ボタン
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("表示中を全て 0"))
        {
            SetAllFilteredValues(0f);
        }
        if (GUILayout.Button("表示中を全て 100"))
        {
            SetAllFilteredValues(100f);
        }
        if (GUILayout.Button("値をリセット"))
        {
            CacheBlendShapeValues();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // BlendShapeリスト
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        foreach (var (index, name) in filteredBlendShapes)
        {
            DrawBlendShapeSlider(index, name);
        }

        EditorGUILayout.EndScrollView();
    }

    private void UpdateFilteredBlendShapes()
    {
        if (lastQuery == searchQuery && lastRenderer == targetRenderer) return;
        
        lastQuery = searchQuery;
        lastRenderer = targetRenderer;
        filteredBlendShapes.Clear();

        if (targetRenderer == null || targetRenderer.sharedMesh == null) return;

        var mesh = targetRenderer.sharedMesh;
        int count = mesh.blendShapeCount;

        // 検索クエリをパース
        var (includeTerms, excludeTerms) = ParseSearchQuery(searchQuery);

        for (int i = 0; i < count; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            string nameLower = name.ToLowerInvariant();

            // 除外チェック
            bool excluded = excludeTerms.Any(term => nameLower.Contains(term));
            if (excluded) continue;

            // 含むチェック (OR検索: いずれかにマッチ、または検索語がなければ全て表示)
            bool included = includeTerms.Count == 0 || 
                           includeTerms.Any(term => nameLower.Contains(term));
            
            if (included)
            {
                filteredBlendShapes.Add((i, name));
            }
        }
    }

    private (List<string> include, List<string> exclude) ParseSearchQuery(string query)
    {
        var includeTerms = new List<string>();
        var excludeTerms = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
            return (includeTerms, excludeTerms);

        var terms = query.ToLowerInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var term in terms)
        {
            if (term.StartsWith("-") && term.Length > 1)
            {
                excludeTerms.Add(term.Substring(1));
            }
            else if (!term.StartsWith("-"))
            {
                includeTerms.Add(term);
            }
        }

        return (includeTerms, excludeTerms);
    }

    private void DrawBlendShapeSlider(int index, string name)
    {
        float currentValue = blendShapeValues.ContainsKey(index) ? blendShapeValues[index] : 0f;
        
        EditorGUILayout.BeginHorizontal();
        
        // 左: 名前ラベル (クリックで0/100トグル)
        var labelStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true
        };
        string displayName = currentValue > 0 ? $"<color=#88ff88>{name}</color>" : name;
        
        if (GUILayout.Button(displayName, labelStyle, GUILayout.Width(180)))
        {
            float newValue = currentValue > 0 ? 0f : 100f;
            SetBlendShapeValue(index, newValue);
        }
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

        // 中央: スライダーバー
        EditorGUI.BeginChangeCheck();
        float sliderValue = GUILayout.HorizontalSlider(currentValue, 0f, 100f, GUILayout.MinWidth(100));
        if (EditorGUI.EndChangeCheck())
        {
            SetBlendShapeValue(index, sliderValue);
        }

        // 右: 数値入力
        EditorGUI.BeginChangeCheck();
        float fieldValue = EditorGUILayout.FloatField(currentValue, GUILayout.Width(50));
        if (EditorGUI.EndChangeCheck())
        {
            SetBlendShapeValue(index, Mathf.Clamp(fieldValue, 0f, 100f));
        }

        EditorGUILayout.EndHorizontal();
    }

    private void SetBlendShapeValue(int index, float value)
    {
        if (targetRenderer == null) return;
        
        Undo.RecordObject(targetRenderer, "Change BlendShape");
        targetRenderer.SetBlendShapeWeight(index, value);
        blendShapeValues[index] = value;
        EditorUtility.SetDirty(targetRenderer);
    }

    private void SetAllFilteredValues(float value)
    {
        if (targetRenderer == null) return;
        
        Undo.RecordObject(targetRenderer, "Change All BlendShapes");
        
        foreach (var (index, _) in filteredBlendShapes)
        {
            targetRenderer.SetBlendShapeWeight(index, value);
            blendShapeValues[index] = value;
        }
        
        EditorUtility.SetDirty(targetRenderer);
    }
}
