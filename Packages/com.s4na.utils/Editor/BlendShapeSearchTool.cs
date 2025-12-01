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
    private List<(int index, string name, string tags)> filteredBlendShapes = new List<(int, string, string)>();
    private string lastQuery = null;
    private SkinnedMeshRenderer lastRenderer = null;

    // ヒットしたタグ一覧（クリック用）
    private Dictionary<string, int> hitTags = new Dictionary<string, int>();

    // 同義語グループ
    private static readonly string[][] synonymGroups = new string[][]
    {
        // === 目関連 ===
        new[] { "eye", "目", "め" },
        new[] { "eyelid", "瞼", "まぶた" },
        new[] { "eyelash", "睫毛", "まつげ", "まつ毛" },
        new[] { "iris", "pupil", "瞳", "ひとみ" },
        new[] { "blink", "まばたき", "瞬き", "目閉じ" },
        new[] { "highlight", "ハイライト", "ハイライ" },
        
        // === 目の形状 ===
        new[] { "tare", "たれ", "タレ", "垂れ" },
        new[] { "turi", "つり", "ツリ", "吊り" },
        new[] { "zito", "じと", "ジト" },
        new[] { "nagomi", "なごみ", "和み", "ナゴミ" },
        new[] { "wink", "ウィンク", "ウインク" },
        new[] { "sleepy", "眠", "ねむ" },
        
        // === 口関連 ===
        new[] { "mouth", "mouse", "口", "くち" },
        new[] { "lip", "唇", "くちびる" },
        new[] { "tongue", "tang", "舌", "した", "べろ", "ベロ" },
        new[] { "tooth", "teeth", "歯", "は" },
        new[] { "yaeba", "八重歯", "やえば" },
        
        // === 顔パーツ ===
        new[] { "brow", "eyebrow", "眉", "まゆ", "眉毛" },
        new[] { "cheek", "頬", "ほほ", "ほっぺ" },
        new[] { "nose", "鼻", "はな" },
        new[] { "tear", "涙", "なみだ", "泪" },
        new[] { "forehead", "額", "おでこ", "ひたい" },
        
        // === 表情 ===
        new[] { "smile", "笑", "えみ", "にこ", "ニコ" },
        new[] { "happy", "joy", "喜", "よろこ", "嬉", "うれ" },
        new[] { "angry", "怒", "おこ", "いか" },
        new[] { "sad", "悲", "かな" },
        new[] { "cry", "泣", "なき", "ないて" },
        new[] { "surprise", "odoroki", "驚", "おどろ", "びっくり" },
        new[] { "fear", "恐", "こわ", "怖" },
        
        // === 日本語表現 ===
        new[] { "nikori", "nikkori", "にこり", "にっこり", "ニコリ" },
        new[] { "niyari", "にやり", "ニヤリ", "にんまり" },
        new[] { "pero", "ぺろ", "ペロ", "舌出" },
        new[] { "puku", "ぷく", "プク", "膨" },
        new[] { "tere", "照", "てれ", "テレ" },
        new[] { "ahaha", "あはは", "アハハ" },
        new[] { "ehehe", "えへへ", "エヘヘ" },
        
        // === 位置 ===
        new[] { "upper", "上", "うえ" },
        new[] { "lower", "下", "した" },
        new[] { "left", "左", "ひだり" },
        new[] { "right", "右", "みぎ" },
        
        // === 形状 ===
        new[] { "big", "大", "おお" },
        new[] { "small", "小", "ちい", "しょう" },
        new[] { "narrow", "細", "ほそ", "狭" },
        new[] { "wide", "広", "ひろ" },
        new[] { "open", "開", "あけ", "ひら" },
        new[] { "close", "閉", "とじ" },
        new[] { "maru", "丸", "まる" },
        new[] { "sharp", "尖", "とが" },
    };

    private static Dictionary<string, HashSet<string>> synonymMap;

    // タグ表示用辞書（英語キー → 日本語表示）
    private static readonly Dictionary<string, string> tagDictionary = new Dictionary<string, string>
    {
        { "eye", "目" },
        { "eyelid", "瞼" },
        { "eyelash", "睫毛" },
        { "iris", "瞳" },
        { "pupil", "瞳" },
        { "mouth", "口" },
        { "mouse", "口" },
        { "tooth", "歯" },
        { "teeth", "歯" },
        { "tongue", "舌" },
        { "tang", "舌" },
        { "brow", "眉" },
        { "eyebrow", "眉" },
        { "cheek", "頬" },
        { "nose", "鼻" },
        { "lip", "唇" },
        { "tear", "涙" },
        { "forehead", "額" },
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
        { "tare", "タレ目" },
        { "turi", "ツリ目" },
        { "nagomi", "なごみ" },
        { "zito", "ジト目" },
        { "highlight", "ハイライト" },
        { "yaeba", "八重歯" },
    };

    // タグのソート順（よく使うものを前に）
    private static readonly List<string> tagOrder = new List<string>
    {
        "目", "口", "眉", "瞳", "瞼", "睫毛", "舌", "歯", "八重歯",
        "頬", "鼻", "唇", "涙", "額",
        "まばたき", "ウィンク", "ジト目", "タレ目", "ツリ目", "なごみ", "眠い", "ハイライト",
        "笑顔", "喜び", "怒り", "悲しみ", "泣き", "驚き"
    };

    static BlendShapeSearchTool()
    {
        BuildSynonymMap();
    }

    private static void BuildSynonymMap()
    {
        synonymMap = new Dictionary<string, HashSet<string>>();
        
        foreach (var group in synonymGroups)
        {
            var groupSet = new HashSet<string>();
            foreach (var word in group)
            {
                groupSet.Add(word.ToLowerInvariant());
            }
            
            foreach (var word in group)
            {
                string key = word.ToLowerInvariant();
                if (!synonymMap.ContainsKey(key))
                {
                    synonymMap[key] = new HashSet<string>(groupSet);
                }
                else
                {
                    foreach (var w in groupSet)
                    {
                        synonymMap[key].Add(w);
                    }
                }
            }
        }
    }

    private HashSet<string> GetSynonyms(string term)
    {
        string termLower = term.ToLowerInvariant();
        var result = new HashSet<string> { termLower };
        
        if (synonymMap.TryGetValue(termLower, out var synonyms))
        {
            foreach (var syn in synonyms)
            {
                result.Add(syn);
            }
            return result;
        }
        
        foreach (var group in synonymGroups)
        {
            bool matched = false;
            foreach (var word in group)
            {
                string wordLower = word.ToLowerInvariant();
                if (wordLower.Contains(termLower) || termLower.Contains(wordLower))
                {
                    matched = true;
                    break;
                }
            }
            
            if (matched)
            {
                foreach (var word in group)
                {
                    result.Add(word.ToLowerInvariant());
                }
            }
        }
        
        return result;
    }

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
            "・じと → 「zito」「ジト」もヒット\n" +
            "・目 -blink → 「eye」含む、まばたき除外\n" +
            "・smile 笑 → どちらかを含む",
            MessageType.None);

        EditorGUILayout.Space(3);

        // 検索実行（タグ一覧も更新される）
        UpdateFilteredBlendShapes();

        // ★ ヒットしたタグ一覧を表示 ★
        DrawHitTagButtons();

        EditorGUILayout.Space(3);

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

    /// <summary>
    /// ヒットしたタグをボタンとして表示し、クリックで絞り込み
    /// </summary>
    private void DrawHitTagButtons()
    {
        if (hitTags.Count == 0) return;

        EditorGUILayout.Space(3);
        
        // タグをソート順に並べる
        var sortedTags = hitTags
            .OrderBy(kvp => {
                int idx = tagOrder.IndexOf(kvp.Key);
                return idx >= 0 ? idx : 999;
            })
            .ThenBy(kvp => kvp.Key)
            .ToList();

        // ラベル
        EditorGUILayout.BeginHorizontal();
        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = Color.gray }
        };
        EditorGUILayout.LabelField("タグで絞込:", labelStyle, GUILayout.Width(65));

        // タグボタンを横並びで表示（Wrap対応）
        float availableWidth = EditorGUIUtility.currentViewWidth - 80;
        float currentLineWidth = 0;
        bool firstInLine = true;

        var tagButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            normal = { textColor = new Color(0.2f, 0.5f, 0.8f) },
            hover = { textColor = new Color(0.3f, 0.6f, 0.9f) },
            padding = new RectOffset(6, 6, 2, 2),
            margin = new RectOffset(2, 2, 0, 0)
        };

        foreach (var kvp in sortedTags)
        {
            string tagText = $"{kvp.Key} ({kvp.Value})";
            float buttonWidth = tagButtonStyle.CalcSize(new GUIContent(tagText)).x + 8;

            // 行の幅を超えたら改行
            if (!firstInLine && currentLineWidth + buttonWidth > availableWidth)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(65); // ラベル分のスペース
                currentLineWidth = 0;
                firstInLine = true;
            }

            if (GUILayout.Button(tagText, tagButtonStyle, GUILayout.Width(buttonWidth)))
            {
                // タグをクリックしたら検索クエリに追加
                OnTagClicked(kvp.Key);
            }

            currentLineWidth += buttonWidth;
            firstInLine = false;
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// タグがクリックされたときの処理
    /// </summary>
    private void OnTagClicked(string tag)
    {
        // タグに対応する検索キーワードを取得
        string searchTerm = GetSearchTermForTag(tag);
        
        // 既存のクエリに追加するか、置き換えるか
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            searchQuery = searchTerm;
        }
        else if (!searchQuery.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant()))
        {
            // まだ含まれていなければ追加
            searchQuery = searchTerm;  // 置き換え（絞り込み用途なので）
        }
        
        lastQuery = null; // 検索を再実行
        GUI.FocusControl(null);
        Repaint();
    }

    /// <summary>
    /// 日本語タグから検索用キーワードを取得
    /// </summary>
    private string GetSearchTermForTag(string japaneseTag)
    {
        // tagDictionaryから逆引き（最初に見つかったキーを返す）
        foreach (var kvp in tagDictionary)
        {
            if (kvp.Value == japaneseTag)
            {
                return kvp.Key;
            }
        }
        // 見つからなければそのまま返す
        return japaneseTag;
    }

    private void UpdateFilteredBlendShapes()
    {
        if (lastQuery == searchQuery && lastRenderer == targetRenderer) return;
        
        lastQuery = searchQuery;
        lastRenderer = targetRenderer;
        filteredBlendShapes.Clear();
        hitTags.Clear(); // タグ一覧もクリア

        if (targetRenderer == null || targetRenderer.sharedMesh == null) return;

        var mesh = targetRenderer.sharedMesh;
        int count = mesh.blendShapeCount;

        var (includeTerms, excludeTerms) = ParseSearchQuery(searchQuery);
        
        // 検索語を同義語で展開
        var expandedIncludeTerms = new List<HashSet<string>>();
        foreach (var term in includeTerms)
        {
            expandedIncludeTerms.Add(GetSynonyms(term));
        }
        
        var expandedExcludeTerms = new HashSet<string>();
        foreach (var term in excludeTerms)
        {
            foreach (var syn in GetSynonyms(term))
            {
                expandedExcludeTerms.Add(syn);
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
                
                // ★ ヒットしたタグをカウント ★
                CollectHitTags(name);
            }
        }
    }

    /// <summary>
    /// BlendShape名からヒットしたタグを収集
    /// </summary>
    private void CollectHitTags(string blendShapeName)
    {
        string nameLower = blendShapeName.ToLowerInvariant();
        var addedTags = new HashSet<string>(); // 重複防止

        foreach (var kvp in tagDictionary)
        {
            if (nameLower.Contains(kvp.Key.ToLowerInvariant()))
            {
                string displayTag = kvp.Value;
                if (!addedTags.Contains(displayTag))
                {
                    addedTags.Add(displayTag);
                    if (hitTags.ContainsKey(displayTag))
                    {
                        hitTags[displayTag]++;
                    }
                    else
                    {
                        hitTags[displayTag] = 1;
                    }
                }
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

        // タグ表示（こちらもクリック可能に）
        if (!string.IsNullOrEmpty(tags))
        {
            var tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.7f, 0.9f) },
                fontSize = 10
            };
            
            // タグを分割してボタン化
            var tagParts = tags.Split(' ');
            foreach (var tagPart in tagParts)
            {
                if (GUILayout.Button(tagPart, tagStyle, GUILayout.ExpandWidth(false)))
                {
                    OnTagClicked(tagPart);
                }
                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
            }
            
            // 残りのスペースを埋める
            float usedWidth = tagParts.Sum(t => tagStyle.CalcSize(new GUIContent(t)).x + 4);
            if (usedWidth < 100)
            {
                GUILayout.Space(100 - usedWidth);
            }
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
