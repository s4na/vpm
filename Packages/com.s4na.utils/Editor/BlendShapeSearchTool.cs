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
    using UnityEngine;using UnityEngine;
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
    private List<(int index, string name, string tags)> filteredBlendShapes = new List<(int, string, string)>();
    private string lastQuery = null;
    private SkinnedMeshRenderer lastRenderer = null;

    // 検索エイリアス: 「この単語で検索したら、これらも一緒に検索する」
    // 連鎖しない。直接マッピングのみ。
    private static readonly Dictionary<string, string[]> searchAliases = new Dictionary<string, string[]>
    {
        // 日本語 → 英語（普通の人が日本語で検索しそうなもの）
        { "目", new[] { "eye" } },
        { "瞼", new[] { "eyelid" } },
        { "まぶた", new[] { "eyelid" } },
        { "まつげ", new[] { "eyelash" } },
        { "睫毛", new[] { "eyelash" } },
        { "瞳", new[] { "iris", "pupil" } },
        { "口", new[] { "mouth", "mouse" } },  // mouseはtypoだけど実際に使われてる
        { "くち", new[] { "mouth", "mouse" } },
        { "歯", new[] { "tooth", "teeth" } },
        { "舌", new[] { "tongue", "tang" } },
        { "眉", new[] { "brow" } },
        { "眉毛", new[] { "brow", "eyebrow" } },
        { "頬", new[] { "cheek" } },
        { "ほっぺ", new[] { "cheek" } },
        { "鼻", new[] { "nose" } },
        { "涙", new[] { "tear", "cry" } },
        
        // 表情系
        { "笑顔", new[] { "smile", "happy" } },
        { "笑い", new[] { "smile" } },
        { "怒り", new[] { "angry" } },
        { "悲しい", new[] { "sad" } },
        { "驚き", new[] { "surprise", "odoroki" } },
        { "まばたき", new[] { "blink" } },
        { "瞬き", new[] { "blink" } },
        { "ウィンク", new[] { "wink" } },
        
        // 英語 → 日本語（英語で検索した時に日本語名もヒットさせる）
        { "eye", new[] { "目" } },
        { "mouth", new[] { "口" } },
        { "brow", new[] { "眉" } },
        { "cheek", new[] { "頬" } },
        { "smile", new[] { "笑", "にこ" } },
        { "angry", new[] { "怒" } },
        { "sad", new[] { "悲" } },
        { "blink", new[] { "まばたき" } },
        { "tear", new[] { "涙", "泣" } },
    };

    // タグ表示用: BlendShape名に含まれる英語 → 日本語タグ
    private static readonly Dictionary<string, string> tagDictionary = new Dictionary<string, string>
    {
        // 部位
        { "eye", "目" },
        { "eyelid", "瞼" },
        { "eyelash", "睫毛" },
        { "iris", "瞳" },
        { "pupil", "瞳孔" },
        { "mouth", "口" },
        { "mouse", "口" },
        { "tooth", "歯" },
        { "teeth", "歯" },
        { "tongue", "舌" },
        { "tang", "舌" },
        { "brow", "眉" },
        { "cheek", "頬" },
        { "nose", "鼻" },
        { "lip", "唇" },
        { "tear", "涙" },
        { "chin", "顎" },
        { "forehead", "額" },
        
        // 表情
        { "blink", "まばたき" },
        { "smile", "笑顔" },
        { "happy", "喜び" },
        { "joy", "喜び" },
        { "angry", "怒り" },
        { "sad", "悲しみ" },
        { "cry", "泣き" },
        { "surprise", "驚き" },
        { "odoroki", "驚き" },
        { "wink", "ウィンク" },
        { "sleepy", "眠い" },
        
        // 目の形
        { "tare", "タレ目" },
        { "turi", "ツリ目" },
        { "nagomi", "なごみ" },
        { "zito", "ジト目" },
        
        // 位置
        { "upper", "上" },
        { "lower", "下" },
        { "left", "左" },
        { "right", "右" },
        
        // VRC
        { "vrc", "VRC" },
    };

    [MenuItem("Tools/VRChat/BlendShape Search Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<BlendShapeSearchTool>("BlendShape Search");
        window.minSize = new Vector2(500, 400);
    }

    [MenuItem("CONTEXT/SkinnedMeshRenderer/BlendShape Search Tool")]
    private static void OpenFromContext(MenuCommand command)
    {
        var renderer = command.context as SkinnedMeshRenderer;
        var window = GetWindow<BlendShapeSearchTool>("BlendShape Search");
        window.minSize = new Vector2(500, 400);
        
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
                lastQuery = null;
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
        
        EditorGUILayout.LabelField("BlendShape Search Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

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
            lastQuery = null;
        }
        if (GUILayout.Button("✕", GUILayout.Width(25)))
        {
            searchQuery = "";
            lastQuery = null;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "検索例:\n" +
            "・eye mouth → 「eye」または「mouth」を含む\n" +
            "・eye -blink → 「eye」を含み「blink」を含まない\n" +
            "・目 → 「eye」もヒット / 口 → 「mouth」もヒット",
            MessageType.None);

        EditorGUILayout.Space(5);

        UpdateFilteredBlendShapes();

        int totalCount = targetRenderer.sharedMesh.blendShapeCount;
        EditorGUILayout.LabelField($"表示: {filteredBlendShapes.Count} / {totalCount}", EditorStyles.miniLabel);

        EditorGUILayout.Space(3);

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

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        foreach (var (index, name, tags) in filteredBlendShapes)
        {
            DrawBlendShapeSlider(index, name, tags);
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

        var (includeTerms, excludeTerms) = ParseSearchQuery(searchQuery);
        
        // 検索語を展開（元の語 + エイリアス）
        var expandedIncludeTerms = new List<HashSet<string>>();
        foreach (var term in includeTerms)
        {
            var termSet = new HashSet<string> { term.ToLowerInvariant() };
            
            // エイリアスがあれば追加（連鎖はしない）
            if (searchAliases.TryGetValue(term.ToLowerInvariant(), out var aliases))
            {
                foreach (var alias in aliases)
                {
                    termSet.Add(alias.ToLowerInvariant());
                }
            }
            
            expandedIncludeTerms.Add(termSet);
        }
        
        // 除外語も同様に展開
        var expandedExcludeTerms = new HashSet<string>();
        foreach (var term in excludeTerms)
        {
            expandedExcludeTerms.Add(term.ToLowerInvariant());
            
            if (searchAliases.TryGetValue(term.ToLowerInvariant(), out var aliases))
            {
                foreach (var alias in aliases)
                {
                    expandedExcludeTerms.Add(alias.ToLowerInvariant());
                }
            }
        }

        for (int i = 0; i < count; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            string nameLower = name.ToLowerInvariant();

            // 除外チェック
            bool excluded = expandedExcludeTerms.Any(term => nameLower.Contains(term));
            if (excluded) continue;

            // 含むチェック
            bool included = expandedIncludeTerms.Count == 0 || 
                           expandedIncludeTerms.Any(termSet => 
                               termSet.Any(term => nameLower.Contains(term)));
            
            if (included)
            {
                string tags = GenerateTags(name);
                filteredBlendShapes.Add((i, name, tags));
            }
        }
    }

    private string GenerateTags(string blendShapeName)
    {
        var tags = new List<string>();
        string nameLower = blendShapeName.ToLowerInvariant();

        foreach (var kvp in tagDictionary)
        {
            if (nameLower.Contains(kvp.Key.ToLowerInvariant()))
            {
                if (!tags.Contains(kvp.Value))
                {
                    tags.Add(kvp.Value);
                }
            }
        }

        // 最大3つまで
        if (tags.Count > 3)
        {
            return string.Join(" ", tags.Take(3));
        }
        
        return string.Join(" ", tags);
    }

    private (List<string> include, List<string> exclude) ParseSearchQuery(string query)
    {
        var includeTerms = new List<string>();
        var excludeTerms = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
            return (includeTerms, excludeTerms);

        var terms = query.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

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

    private void DrawBlendShapeSlider(int index, string name, string tags)
    {
        float currentValue = blendShapeValues.ContainsKey(index) ? blendShapeValues[index] : 0f;
        
        EditorGUILayout.BeginHorizontal();
        
        // 名前ラベル
        var labelStyle = new GUIStyle(EditorStyles.label) { richText = true };
        string displayName = currentValue > 0 ? $"<color=#88ff88>{name}</color>" : name;
        
        if (GUILayout.Button(displayName, labelStyle, GUILayout.Width(200)))
        {
            float newValue = currentValue > 0 ? 0f : 100f;
            SetBlendShapeValue(index, newValue);
        }
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

        // タグ表示
        if (!string.IsNullOrEmpty(tags))
        {
            var tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.7f, 0.9f) },
                fontSize = 10
            };
            EditorGUILayout.LabelField(tags, tagStyle, GUILayout.Width(100));
        }
        else
        {
            GUILayout.Space(100);
        }

        // スライダー
        EditorGUI.BeginChangeCheck();
        float sliderValue = GUILayout.HorizontalSlider(currentValue, 0f, 100f, GUILayout.MinWidth(100));
        if (EditorGUI.EndChangeCheck())
        {
            SetBlendShapeValue(index, sliderValue);
        }

        // 数値
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
        
        foreach (var (index, _, _) in filteredBlendShapes)
        {
            targetRenderer.SetBlendShapeWeight(index, value);
            blendShapeValues[index] = value;
        }
        
        EditorUtility.SetDirty(targetRenderer);
    }
}

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
    private List<(int index, string name, string tags)> filteredBlendShapes = new List<(int, string, string)>();
    private string lastQuery = null;
    private SkinnedMeshRenderer lastRenderer = null;

    // 検索エイリアス: 「この単語で検索したら、これらも一緒に検索する」
    // 連鎖しない。直接マッピングのみ。
    private static readonly Dictionary<string, string[]> searchAliases = new Dictionary<string, string[]>
    {
        // 日本語 → 英語（普通の人が日本語で検索しそうなもの）
        { "目", new[] { "eye" } },
        { "瞼", new[] { "eyelid" } },
        { "まぶた", new[] { "eyelid" } },
        { "まつげ", new[] { "eyelash" } },
        { "睫毛", new[] { "eyelash" } },
        { "瞳", new[] { "iris", "pupil" } },
        { "口", new[] { "mouth", "mouse" } },  // mouseはtypoだけど実際に使われてる
        { "くち", new[] { "mouth", "mouse" } },
        { "歯", new[] { "tooth", "teeth" } },
        { "舌", new[] { "tongue", "tang" } },
        { "眉", new[] { "brow" } },
        { "眉毛", new[] { "brow", "eyebrow" } },
        { "頬", new[] { "cheek" } },
        { "ほっぺ", new[] { "cheek" } },
        { "鼻", new[] { "nose" } },
        { "涙", new[] { "tear", "cry" } },
        
        // 表情系
        { "笑顔", new[] { "smile", "happy" } },
        { "笑い", new[] { "smile" } },
        { "怒り", new[] { "angry" } },
        { "悲しい", new[] { "sad" } },
        { "驚き", new[] { "surprise", "odoroki" } },
        { "まばたき", new[] { "blink" } },
        { "瞬き", new[] { "blink" } },
        { "ウィンク", new[] { "wink" } },
        
        // 英語 → 日本語（英語で検索した時に日本語名もヒットさせる）
        { "eye", new[] { "目" } },
        { "mouth", new[] { "口" } },
        { "brow", new[] { "眉" } },
        { "cheek", new[] { "頬" } },
        { "smile", new[] { "笑", "にこ" } },
        { "angry", new[] { "怒" } },
        { "sad", new[] { "悲" } },
        { "blink", new[] { "まばたき" } },
        { "tear", new[] { "涙", "泣" } },
    };

    // タグ表示用: BlendShape名に含まれる英語 → 日本語タグ
    private static readonly Dictionary<string, string> tagDictionary = new Dictionary<string, string>
    {
        // 部位
        { "eye", "目" },
        { "eyelid", "瞼" },
        { "eyelash", "睫毛" },
        { "iris", "瞳" },
        { "pupil", "瞳孔" },
        { "mouth", "口" },
        { "mouse", "口" },
        { "tooth", "歯" },
        { "teeth", "歯" },
        { "tongue", "舌" },
        { "tang", "舌" },
        { "brow", "眉" },
        { "cheek", "頬" },
        { "nose", "鼻" },
        { "lip", "唇" },
        { "tear", "涙" },
        { "chin", "顎" },
        { "forehead", "額" },
        
        // 表情
        { "blink", "まばたき" },
        { "smile", "笑顔" },
        { "happy", "喜び" },
        { "joy", "喜び" },
        { "angry", "怒り" },
        { "sad", "悲しみ" },
        { "cry", "泣き" },
        { "surprise", "驚き" },
        { "odoroki", "驚き" },
        { "wink", "ウィンク" },
        { "sleepy", "眠い" },
        
        // 目の形
        { "tare", "タレ目" },
        { "turi", "ツリ目" },
        { "nagomi", "なごみ" },
        { "zito", "ジト目" },
        
        // 位置
        { "upper", "上" },
        { "lower", "下" },
        { "left", "左" },
        { "right", "右" },
        
        // VRC
        { "vrc", "VRC" },
    };

    [MenuItem("Tools/VRChat/BlendShape Search Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<BlendShapeSearchTool>("BlendShape Search");
        window.minSize = new Vector2(500, 400);
    }

    [MenuItem("CONTEXT/SkinnedMeshRenderer/BlendShape Search Tool")]
    private static void OpenFromContext(MenuCommand command)
    {
        var renderer = command.context as SkinnedMeshRenderer;
        var window = GetWindow<BlendShapeSearchTool>("BlendShape Search");
        window.minSize = new Vector2(500, 400);
        
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
                lastQuery = null;
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
        
        EditorGUILayout.LabelField("BlendShape Search Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

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
            lastQuery = null;
        }
        if (GUILayout.Button("✕", GUILayout.Width(25)))
        {
            searchQuery = "";
            lastQuery = null;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "検索例:\n" +
            "・eye mouth → 「eye」または「mouth」を含む\n" +
            "・eye -blink → 「eye」を含み「blink」を含まない\n" +
            "・目 → 「eye」もヒット / 口 → 「mouth」もヒット",
            MessageType.None);

        EditorGUILayout.Space(5);

        UpdateFilteredBlendShapes();

        int totalCount = targetRenderer.sharedMesh.blendShapeCount;
        EditorGUILayout.LabelField($"表示: {filteredBlendShapes.Count} / {totalCount}", EditorStyles.miniLabel);

        EditorGUILayout.Space(3);

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

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        foreach (var (index, name, tags) in filteredBlendShapes)
        {
            DrawBlendShapeSlider(index, name, tags);
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

        var (includeTerms, excludeTerms) = ParseSearchQuery(searchQuery);
        
        // 検索語を展開（元の語 + エイリアス）
        var expandedIncludeTerms = new List<HashSet<string>>();
        foreach (var term in includeTerms)
        {
            var termSet = new HashSet<string> { term.ToLowerInvariant() };
            
            // エイリアスがあれば追加（連鎖はしない）
            if (searchAliases.TryGetValue(term.ToLowerInvariant(), out var aliases))
            {
                foreach (var alias in aliases)
                {
                    termSet.Add(alias.ToLowerInvariant());
                }
            }
            
            expandedIncludeTerms.Add(termSet);
        }
        
        // 除外語も同様に展開
        var expandedExcludeTerms = new HashSet<string>();
        foreach (var term in excludeTerms)
        {
            expandedExcludeTerms.Add(term.ToLowerInvariant());
            
            if (searchAliases.TryGetValue(term.ToLowerInvariant(), out var aliases))
            {
                foreach (var alias in aliases)
                {
                    expandedExcludeTerms.Add(alias.ToLowerInvariant());
                }
            }
        }

        for (int i = 0; i < count; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            string nameLower = name.ToLowerInvariant();

            // 除外チェック
            bool excluded = expandedExcludeTerms.Any(term => nameLower.Contains(term));
            if (excluded) continue;

            // 含むチェック
            bool included = expandedIncludeTerms.Count == 0 || 
                           expandedIncludeTerms.Any(termSet => 
                               termSet.Any(term => nameLower.Contains(term)));
            
            if (included)
            {
                string tags = GenerateTags(name);
                filteredBlendShapes.Add((i, name, tags));
            }
        }
    }

    private string GenerateTags(string blendShapeName)
    {
        var tags = new List<string>();
        string nameLower = blendShapeName.ToLowerInvariant();

        foreach (var kvp in tagDictionary)
        {
            if (nameLower.Contains(kvp.Key.ToLowerInvariant()))
            {
                if (!tags.Contains(kvp.Value))
                {
                    tags.Add(kvp.Value);
                }
            }
        }

        // 最大3つまで
        if (tags.Count > 3)
        {
            return string.Join(" ", tags.Take(3));
        }
        
        return string.Join(" ", tags);
    }

    private (List<string> include, List<string> exclude) ParseSearchQuery(string query)
    {
        var includeTerms = new List<string>();
        var excludeTerms = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
            return (includeTerms, excludeTerms);

        var terms = query.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

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

    private void DrawBlendShapeSlider(int index, string name, string tags)
    {
        float currentValue = blendShapeValues.ContainsKey(index) ? blendShapeValues[index] : 0f;
        
        EditorGUILayout.BeginHorizontal();
        
        // 名前ラベル
        var labelStyle = new GUIStyle(EditorStyles.label) { richText = true };
        string displayName = currentValue > 0 ? $"<color=#88ff88>{name}</color>" : name;
        
        if (GUILayout.Button(displayName, labelStyle, GUILayout.Width(200)))
        {
            float newValue = currentValue > 0 ? 0f : 100f;
            SetBlendShapeValue(index, newValue);
        }
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

        // タグ表示
        if (!string.IsNullOrEmpty(tags))
        {
            var tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.7f, 0.9f) },
                fontSize = 10
            };
            EditorGUILayout.LabelField(tags, tagStyle, GUILayout.Width(100));
        }
        else
        {
            GUILayout.Space(100);
        }

        // スライダー
        EditorGUI.BeginChangeCheck();
        float sliderValue = GUILayout.HorizontalSlider(currentValue, 0f, 100f, GUILayout.MinWidth(100));
        if (EditorGUI.EndChangeCheck())
        {
            SetBlendShapeValue(index, sliderValue);
        }

        // 数値
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
        
        foreach (var (index, _, _) in filteredBlendShapes)
        {
            targetRenderer.SetBlendShapeWeight(index, value);
            blendShapeValues[index] = value;
        }
        
        EditorUtility.SetDirty(targetRenderer);
    }
}

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
