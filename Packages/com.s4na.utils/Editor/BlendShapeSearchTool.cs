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
        new[] { "eyelash", "eyelashes", "睫毛", "まつげ", "まつ毛" },
        new[] { "iris", "虹彩", "こうさい" },
        new[] { "pupil", "瞳孔", "どうこう" },
        new[] { "blink", "まばたき", "瞬き", "目閉じ" },
        new[] { "catchlight", "highlight", "ハイライト", "ハイライ", "キャッチライト" },
        
        // === 目の形状 ===
        new[] { "tare", "たれ", "タレ", "垂れ" },
        new[] { "turi", "つり", "ツリ", "吊り" },
        new[] { "jito", "zito", "じと", "ジト" },
        new[] { "nagomi", "なごみ", "和み", "ナゴミ" },
        new[] { "wink", "ウィンク", "ウインク" },
        new[] { "sleepy", "眠", "ねむ" },
        new[] { "doya", "ドヤ", "どや", "得意" },
        new[] { "uruuru", "うるうる", "ウルウル", "潤み" },
        new[] { "kirakira", "キラキラ", "きらきら" },
        new[] { "yandere", "ヤンデレ", "やんでれ", "病み" },
        
        // === 口関連 ===
        new[] { "mouth", "mouse", "口", "くち" },
        new[] { "lip", "唇", "くちびる" },
        new[] { "tongue", "tang", "舌", "した", "べろ", "ベロ" },
        new[] { "tooth", "teeth", "歯", "は" },
        new[] { "yaeba", "八重歯", "やえば" },
        new[] { "giza", "ギザ", "ぎざ", "ギザ歯" },
        new[] { "drool", "よだれ", "ヨダレ", "涎" },
        new[] { "pero", "ぺろ", "ペロ", "舌出" },
        new[] { "puku", "ぷく", "プク", "膨", "ふくれ" },
        new[] { "grin", "にやり", "ニヤリ", "にんまり" },
        
        // === 眉関連 ===
        new[] { "brow", "eyebrow", "eyebrows", "眉", "まゆ", "眉毛" },
        new[] { "maromayu", "まろ眉", "まろまゆ", "丸眉" },
        new[] { "komaru", "困", "こまる", "困り眉" },
        new[] { "annoying", "いらいら", "イライラ" },
        new[] { "straight", "ストレート", "まっすぐ", "直線" },
        new[] { "thick", "太", "ふと", "太い" },
        new[] { "thin", "細", "ほそ", "細い" },
        
        // === 顔パーツ ===
        new[] { "cheek", "頬", "ほほ", "ほっぺ" },
        new[] { "nose", "鼻", "はな" },
        new[] { "tear", "涙", "なみだ", "泪" },
        new[] { "forehead", "額", "おでこ", "ひたい" },
        new[] { "ear", "耳", "みみ" },
        new[] { "elf", "エルフ", "エルフ耳" },
        new[] { "face", "facetype", "顔", "かお", "フェイス" },
        new[] { "chin", "顎", "あご", "アゴ" },
        new[] { "jawline", "顎ライン", "輪郭" },
        new[] { "muzzle", "マズル", "口元" },
        new[] { "head", "頭", "あたま" },
        new[] { "neckline", "首", "くび", "ネックライン" },
        
        // === 顔の形状 ===
        new[] { "chubby", "ぽっちゃり", "ふっくら" },
        new[] { "maru", "丸", "まる" },
        new[] { "sharp", "シャープ", "尖", "とが" },
        new[] { "soft", "ソフト", "柔らか" },
        new[] { "boyish", "ボーイッシュ", "男の子" },
        new[] { "long", "長", "なが" },
        new[] { "short", "短", "みじか" },
        new[] { "fuku", "膨らみ", "ふくらみ" },
        
        // === 表情 ===
        new[] { "smile", "笑", "えみ", "にこ", "ニコ" },
        new[] { "happy", "joy", "喜", "よろこ", "嬉", "うれ" },
        new[] { "angry", "anger", "怒", "おこ", "いか" },
        new[] { "sad", "悲", "かな" },
        new[] { "cry", "泣", "なき", "ないて" },
        new[] { "surprise", "surprised", "odoroki", "驚", "おどろ", "びっくり" },
        new[] { "fear", "恐", "こわ", "怖" },
        
        // === 日本語表現 ===
        new[] { "nikori", "nikkori", "にこり", "にっこり", "ニコリ" },
        new[] { "niyari", "にやり", "ニヤリ", "にんまり" },
        new[] { "tere", "照", "てれ", "テレ" },
        new[] { "ahaha", "あはは", "アハハ" },
        new[] { "ehehe", "えへへ", "エヘヘ" },
        
        // === その他効果 ===
        new[] { "blush", "赤面", "照れ", "頬染め" },
        new[] { "lineblush", "線照れ", "///", "斜線" },
        new[] { "sweat", "汗", "あせ" },
        new[] { "heart", "ハート", "はーと" },
        new[] { "star", "スター", "星", "ほし" },
        new[] { "white_eye", "白目", "しろめ" },
        new[] { "blanched", "青ざめ", "蒼白" },
        
        // === 動物プリセット ===
        new[] { "cat", "猫", "ねこ", "ネコ" },
        new[] { "dog", "犬", "いぬ", "イヌ" },
        new[] { "fox", "狐", "きつね", "キツネ" },
        new[] { "rabbit", "うさぎ", "ウサギ", "兎" },
        new[] { "tanuki", "たぬき", "タヌキ", "狸" },
        new[] { "sloth", "ナマケモノ", "なまけもの" },
        new[] { "goat", "山羊", "やぎ", "ヤギ" },
        
        // === 位置 ===
        new[] { "upper", "上", "うえ" },
        new[] { "lower", "下", "した" },
        new[] { "left", "左", "ひだり", "_l" },
        new[] { "right", "右", "みぎ", "_r" },
        new[] { "front", "前", "まえ" },
        new[] { "back", "後", "うしろ", "奥" },
        new[] { "center", "中央", "センター" },
        new[] { "main", "メイン", "主" },
        new[] { "sub", "サブ", "副" },
        
        // === 形状・サイズ ===
        new[] { "big", "大", "おお" },
        new[] { "small", "小", "ちい", "しょう" },
        new[] { "narrow", "細", "ほそ", "狭" },
        new[] { "wide", "広", "ひろ" },
        new[] { "open", "開", "あけ", "ひら" },
        new[] { "close", "閉", "とじ" },
        new[] { "squash", "つぶれ", "押し潰し" },
        
        // === 回転・変形 ===
        new[] { "rotating", "回転", "かいてん" },
        new[] { "inward", "内側", "うちがわ" },
        new[] { "outward", "外側", "そとがわ" },
        new[] { "sori", "反り", "そり" },
        new[] { "yori", "寄り", "より" },
        new[] { "morph", "モーフ", "変形" },
        
        // === 特殊効果 ===
        new[] { "off", "オフ", "非表示", "消す" },
        new[] { "double", "二重", "ふたえ", "ダブル" },
        new[] { "preset", "プリセット" },
        new[] { "point", "ポイント", "先端" },
        new[] { "line", "ライン", "線" },
        new[] { "shape", "シェイプ", "形状" },
        
        // === VRChat関連 ===
        new[] { "vrc", "vrchat", "ブイアールチャット" },
        new[] { "viseme", "ビゼム", "リップシンク" },
        new[] { "v_aa", "あ", "ア" },
        new[] { "v_ih", "い", "イ" },
        new[] { "v_oh", "お", "オ" },
        new[] { "v_ou", "う", "ウ" },
        new[] { "v_e", "え", "エ" },
        new[] { "lookingup", "上見", "見上げ" },
        new[] { "lookingdown", "下見", "見下ろし" },
    };

    private static Dictionary<string, HashSet<string>> synonymMap;

    // タグ表示用辞書（英語キー → 日本語表示）
    private static readonly Dictionary<string, string> tagDictionary = new Dictionary<string, string>
    {
        // === 目関連 ===
        { "eye", "目" },
        { "eyelid", "瞼" },
        { "eyelash", "睫毛" },
        { "eyelashes", "睫毛" },
        { "iris", "虹彩" },
        { "pupil", "瞳孔" },
        { "catchlight", "ハイライト" },
        { "highlight", "ハイライト" },
        { "blink", "まばたき" },
        
        // === 目の形状 ===
        { "tare", "タレ目" },
        { "turi", "ツリ目" },
        { "jito", "ジト目" },
        { "zito", "ジト目" },
        { "nagomi", "なごみ" },
        { "doya", "ドヤ顔" },
        { "wink", "ウィンク" },
        { "sleepy", "眠い" },
        { "uruuru", "うるうる" },
        { "kirakira", "キラキラ" },
        { "yandere", "ヤンデレ" },
        
        // === 口関連 ===
        { "mouth", "口" },
        { "mouse", "口" },
        { "lip", "唇" },
        { "tongue", "舌" },
        { "tang", "舌" },
        { "tooth", "歯" },
        { "teeth", "歯" },
        { "yaeba", "八重歯" },
        { "giza", "ギザ歯" },
        { "drool", "よだれ" },
        { "pero", "ペロ" },
        { "puku", "ぷくー" },
        { "grin", "ニヤリ" },
        
        // === 眉関連 ===
        { "brow", "眉" },
        { "eyebrow", "眉" },
        { "eyebrows", "眉" },
        { "maromayu", "まろ眉" },
        { "komaru", "困り眉" },
        { "annoying", "イライラ" },
        { "straight", "ストレート" },
        { "thick", "太い" },
        { "thin", "細い" },
        
        // === 顔パーツ ===
        { "cheek", "頬" },
        { "nose", "鼻" },
        { "tear", "涙" },
        { "forehead", "額" },
        { "ear", "耳" },
        { "elf", "エルフ" },
        { "face", "顔" },
        { "facetype", "顔型" },
        { "chin", "顎" },
        { "jawline", "輪郭" },
        { "muzzle", "マズル" },
        { "head", "頭" },
        { "neckline", "首" },
        
        // === 顔の形状 ===
        { "chubby", "ぽっちゃり" },
        { "maru", "丸" },
        { "sharp", "シャープ" },
        { "soft", "ソフト" },
        { "boyish", "ボーイッシュ" },
        { "long", "長い" },
        { "short", "短い" },
        { "fuku", "膨らみ" },
        
        // === 表情 ===
        { "smile", "笑顔" },
        { "happy", "喜び" },
        { "joy", "喜び" },
        { "angry", "怒り" },
        { "anger", "怒り" },
        { "sad", "悲しみ" },
        { "cry", "泣き" },
        { "surprise", "驚き" },
        { "surprised", "驚き" },
        { "odoroki", "驚き" },
        { "fear", "恐怖" },
        
        // === その他効果 ===
        { "blush", "赤面" },
        { "lineblush", "線照れ" },
        { "sweat", "汗" },
        { "heart", "ハート" },
        { "star", "星" },
        { "white_eye", "白目" },
        { "blanched", "青ざめ" },
        
        // === 動物プリセット ===
        { "cat", "猫" },
        { "dog", "犬" },
        { "fox", "狐" },
        { "rabbit", "うさぎ" },
        { "tanuki", "たぬき" },
        { "sloth", "ナマケモノ" },
        { "goat", "山羊" },
        
        // === 位置 ===
        { "upper", "上" },
        { "lower", "下" },
        { "left", "左" },
        { "right", "右" },
        { "front", "前" },
        { "back", "後" },
        { "center", "中央" },
        { "main", "メイン" },
        { "sub", "サブ" },
        
        // === 形状・サイズ ===
        { "big", "大" },
        { "small", "小" },
        { "narrow", "細" },
        { "wide", "広" },
        { "open", "開く" },
        { "close", "閉じる" },
        { "squash", "つぶれ" },
        
        // === 回転・変形 ===
        { "rotating", "回転" },
        { "inward", "内側" },
        { "outward", "外側" },
        { "sori", "反り" },
        { "yori", "寄り" },
        { "morph", "変形" },
        
        // === 特殊効果 ===
        { "off", "OFF" },
        { "double", "二重" },
        { "preset", "プリセット" },
        { "point", "先端" },
        { "line", "ライン" },
        { "shape", "形状" },
        
        // === VRChat関連 ===
        { "vrc", "VRC" },
        { "lookingup", "上見" },
        { "lookingdown", "下見" },
    };

    // タグのソート順（よく使うものを前に）
    private static readonly List<string> tagOrder = new List<string>
    {
        // 顔パーツ
        "目", "口", "眉", "虹彩", "瞳孔", "瞼", "睫毛", "舌", "歯", "八重歯",
        "頬", "鼻", "唇", "涙", "額", "耳", "顔", "顔型", "顎", "輪郭", "マズル", "頭", "首",
        // 目の形状
        "まばたき", "ウィンク", "ジト目", "タレ目", "ツリ目", "なごみ", "ドヤ顔", "眠い", 
        "ハイライト", "うるうる", "キラキラ", "ヤンデレ",
        // 眉の形状
        "まろ眉", "困り眉", "イライラ", "ストレート",
        // 口の形状
        "ギザ歯", "よだれ", "ペロ", "ぷくー", "ニヤリ",
        // 顔の形状
        "ぽっちゃり", "丸", "シャープ", "ソフト", "ボーイッシュ", "長い", "短い", "膨らみ",
        // 表情
        "笑顔", "喜び", "怒り", "悲しみ", "泣き", "驚き", "恐怖",
        // その他効果
        "赤面", "線照れ", "汗", "ハート", "星", "白目", "青ざめ",
        // 動物
        "猫", "犬", "狐", "うさぎ", "たぬき", "ナマケモノ", "山羊", "エルフ",
        // 位置
        "上", "下", "左", "右", "前", "後", "中央", "メイン", "サブ",
        // サイズ・形状
        "大", "小", "細", "広", "太い", "細い", "開く", "閉じる", "つぶれ",
        // 回転・変形
        "回転", "内側", "外側", "反り", "寄り", "変形",
        // 特殊
        "OFF", "二重", "プリセット", "先端", "ライン", "形状",
        // VRC
        "VRC", "上見", "下見"
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

        // ターゲット表示
        EditorGUI.BeginChangeCheck();
        var newTarget = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
            "Target", targetRenderer, typeof(SkinnedMeshRenderer), true);
        if (EditorGUI.EndChangeCheck() && newTarget != targetRenderer)
        {
            targetRenderer = newTarget;
            CacheBlendShapeValues();
            lastQuery = null;
        }

        if (targetRenderer == null)
        {
            EditorGUILayout.HelpBox("SkinnedMeshRendererを選択してください", MessageType.Info);
            return;
        }

        if (targetRenderer.sharedMesh == null)
        {
            EditorGUILayout.HelpBox("メッシュがありません", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(5);

        // 検索ボックス
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("検索", GUILayout.Width(40));
        EditorGUI.BeginChangeCheck();
        searchQuery = EditorGUILayout.TextField(searchQuery);
        if (EditorGUI.EndChangeCheck())
        {
            lastQuery = null;
        }
        if (GUILayout.Button("×", GUILayout.Width(25)))
        {
            searchQuery = "";
            lastQuery = null;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        // ヒント
        EditorGUILayout.LabelField("例: 目, eye, smile, -blink（除外）", EditorStyles.miniLabel);

        EditorGUILayout.Space(5);

        // フィルタ更新
        UpdateFilteredBlendShapes();

        // ★ ヒットしたタグ一覧を表示 ★
        if (hitTags.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("タグ:", GUILayout.Width(35));
            
            // tagOrderに従ってソート
            var sortedTags = hitTags.Keys
                .OrderBy(t => {
                    int idx = tagOrder.IndexOf(t);
                    return idx >= 0 ? idx : int.MaxValue;
                })
                .ToList();
            
            var tagButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2)
            };
            
            foreach (var tag in sortedTags)
            {
                string buttonLabel = $"{tag}({hitTags[tag]})";
                if (GUILayout.Button(buttonLabel, tagButtonStyle, GUILayout.ExpandWidth(false)))
                {
                    OnTagClicked(tag);
                }
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
        }

        // 結果カウント
        int totalCount = targetRenderer.sharedMesh.blendShapeCount;
        EditorGUILayout.LabelField($"表示: {filteredBlendShapes.Count} / {totalCount}", EditorStyles.boldLabel);

        // 一括操作
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("すべて0"))
        {
            SetAllFilteredValues(0f);
        }
        if (GUILayout.Button("すべて100"))
        {
            SetAllFilteredValues(100f);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // BlendShapeリスト
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        foreach (var (index, name, tags) in filteredBlendShapes)
        {
            DrawBlendShapeSlider(index, name, tags);
        }
        
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// タグがクリックされたときの処理
    /// </summary>
    private void OnTagClicked(string clickedTag)
    {
        // 日本語タグからキーワードを取得
        string searchTerm = GetSearchTermForTag(clickedTag);
        
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
