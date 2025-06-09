using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking; // UnityWebRequestを使用するために必要
using System.IO;
using System.Linq; // Take()などのLINQメソッドを使用するために必要
using System;


public class csvdata_sample : MonoBehaviour
{
    [Header("1. データソース")]
    [Tooltip("プロジェクト内にあるCSVファイルを指定します。")]
    [SerializeField] public TextAsset starDataCsv;

    // [Header("Webからの読み込み (上級者向け)")]
    // [Tooltip("CSVファイルの完全なURL。こちらを使用する場合は、上記のStar Data CsvはNoneにしてください。")]
    // [SerializeField] private string csvUrl = "";

    [Header("2. 星の見た目と配置")]
    [Tooltip("星のオブジェクトとして使用するPrefab。")]
    [SerializeField] private GameObject starPrefab;

    [Header("配置方法: 天球座標 (RA/Dec)")]
    [Tooltip("天球座標(RA/Dec)を使う場合の、星を配置する球の半径。")]
    [SerializeField] private float sphereRadius = 100f;

    [Header("配置方法: デカルト座標 (X,Y,Z)")]
    [Tooltip("★★修正点★★ デカルト座標(X,Y,Z)を使う場合の、全体的なスケール倍率。")]
    [SerializeField] private float cartesianScaleMultiplier = 1.0f;

    [Header("共通の見た目設定")]
    [Tooltip("等級(magnitude)を星のスケールに変換する際の係数。")]
    [SerializeField] private float magnitudeScaleFactor = 0.5f;
    [Tooltip("等級からスケールを計算する際の基準値。")]
    [SerializeField] private float magnitudeBaseValue = 6f;
    [Tooltip("計算されたスケールの最小値と最大値。")]
    [SerializeField] private Vector2 minMaxScale = new Vector2(0.05f, 1.0f);
    [Tooltip("星のマテリアルが放つ光の強さ。")]
    [SerializeField] private float emissionIntensity = 1.0f;

    [Header("3. CSV列名とパフォーマンス")]
    [Tooltip("CSVファイル内のデータ列に対応するヘッダー名。")]
    [SerializeField] private CsvColumnNames columnNames = new CsvColumnNames();
    [Tooltip("生成する星の最大数（0なら無制限）。")]
    [SerializeField] private int maxStarsToCreate = 5000;

    // --- 内部変数 ---
    private List<StarData> stars = new List<StarData>();
    private UnityWebRequest webRequest;
    private Dictionary<string, int> headerMap = new Dictionary<string, int>();

    [System.Serializable]
    public class CsvColumnNames
    {
        // 天球座標
        public string rightAscension = "ra";
        public string declination = "dec";
        // デカルト座標
        public string x = "x";
        public string y = "y";
        public string z = "z";
        // その他データ
        public string magnitude = "mag";
        public string colorIndex = "ci";
        public string spectralType = "spect";
        public string properName = "proper";
    }

    [System.Serializable]
    public class StarData
    {
        // ★★修正点★★ 全ての座標データをnull許容型(float?)に変更し、存在チェックを確実に行えるようにする
        public float? rightAscension;
        public float? declination;
        public float? x;
        public float? y;
        public float? z;

        public float magnitude = 6.0f;
        public float? colorIndex;
        public string spectralType = "";
        public string properName = "";
    }

    void Start()
    {
        StartCoroutine(LoadAndProcessData());
    }

    void OnDisable()
    {
        if (webRequest != null && !webRequest.isDone)
        {
            webRequest.Abort();
        }
    }

    private IEnumerator LoadAndProcessData()
    {
        string csvText = "";

        // URL読み込みは現在無効化
        // if (!string.IsNullOrEmpty(csvUrl)) { ... }

        if (starDataCsv != null)
        {
            Debug.Log($"ローカルのTextAsset '{starDataCsv.name}' からCSVを読み込みます。");
            csvText = starDataCsv.text;
        }
        else
        {
            Debug.LogError("データソースが指定されていません。インスペクターで 'Star Data Csv' を設定してください。");
            yield break;
        }

        ProcessCSVData(csvText);
        createStarObjects();
    }

    private void ProcessCSVData(string csvText)
    {
        stars.Clear();
        var warnings = new List<string>();
        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1)
        {
            Debug.LogError("CSVファイルにデータが含まれていないか、ヘッダー行のみです。");
            return;
        }

        ParseHeader(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            if (maxStarsToCreate > 0 && stars.Count >= maxStarsToCreate)
            {
                Debug.Log($"設定された最大星数 {maxStarsToCreate} に達しました。");
                break;
            }

            var values = lines[i].Split(',');
            if (values.Length < 1) continue;

            try
            {
                var star = new StarData();

                // 座標データを読み込む
                star.rightAscension = GetNullableFloatValue(values, columnNames.rightAscension);
                star.declination = GetNullableFloatValue(values, columnNames.declination);
                star.x = GetNullableFloatValue(values, columnNames.x);
                star.y = GetNullableFloatValue(values, columnNames.y);
                star.z = GetNullableFloatValue(values, columnNames.z);

                // 赤経は時単位(hour)から度単位(degree)に変換
                if (star.rightAscension.HasValue)
                {
                    star.rightAscension *= 15f;
                }

                // その他のデータを読み込む
                star.magnitude = GetFloatValue(values, columnNames.magnitude, 6.0f);
                star.colorIndex = GetNullableFloatValue(values, columnNames.colorIndex);
                star.spectralType = GetStringValue(values, columnNames.spectralType);
                star.properName = GetStringValue(values, columnNames.properName);

                stars.Add(star);
            }
            catch (Exception ex)
            {
                warnings.Add($"行 {i + 1} の解析エラー: {ex.Message}. 行の内容: '{lines[i]}'");
            }
        }

        Debug.Log($"CSV処理完了。{stars.Count}個の星データを読み込みました。");
        if (warnings.Count > 0)
        {
            Debug.LogWarning($"{warnings.Count}件の警告があります。最初の5件:\n" + string.Join("\n", warnings.Take(5)));
        }
    }

    private void ParseHeader(string headerLine)
    {
        headerMap.Clear();
        var headers = headerLine.Trim().ToLower().Split(',');
        for (int i = 0; i < headers.Length; i++)
        {
            if (!headerMap.ContainsKey(headers[i]))
            {
                headerMap.Add(headers[i], i);
            }
        }
        Debug.Log("CSVヘッダー解析完了: " + string.Join(", ", headers));

        // ★★修正点★★ どちらの座標系が使えるかチェックし、ログを出す
        bool hasCartesian = headerMap.ContainsKey(columnNames.x.ToLower()) && headerMap.ContainsKey(columnNames.y.ToLower()) && headerMap.ContainsKey(columnNames.z.ToLower());
        bool hasCelestial = headerMap.ContainsKey(columnNames.rightAscension.ToLower()) && headerMap.ContainsKey(columnNames.declination.ToLower());

        if (!hasCartesian && !hasCelestial)
        {
            Debug.LogError("必須の座標列が見つかりません。デカルト座標(x,y,z)または天球座標(ra,dec)のいずれかがCSVヘッダーに必要です。");
        }
        else
        {
            string detected = "検出された座標系: ";
            if (hasCartesian) detected += "デカルト(X,Y,Z) ";
            if (hasCelestial) detected += "天球(RA,Dec)";
            Debug.Log(detected);
        }
    }

    private void createStarObjects()
    {
        if (starPrefab == null)
        {
            Debug.LogWarning("Star Prefabが設定されていません。デフォルトの球体を使用します。");
        }

        GameObject starContainer = new GameObject("Star Container");
        starContainer.transform.SetParent(this.transform, false);
        int createdCount = 0;

        foreach (var starData in stars)
        {
            Vector3 position;

            // デカルト座標(x,y,z)を優先
            if (starData.x.HasValue && starData.y.HasValue && starData.z.HasValue)
            {
                position = new Vector3(starData.x.Value, starData.y.Value, starData.z.Value) * cartesianScaleMultiplier;
            }
            // なければ天球座標(ra,dec)を使用
            else if (starData.rightAscension.HasValue && starData.declination.HasValue)
            {
                float raRad = starData.rightAscension.Value * Mathf.Deg2Rad;
                float decRad = starData.declination.Value * Mathf.Deg2Rad;
                position = new Vector3(
                    sphereRadius * Mathf.Cos(decRad) * Mathf.Cos(raRad),
                    sphereRadius * Mathf.Sin(decRad),
                    sphereRadius * Mathf.Cos(decRad) * Mathf.Sin(raRad)
                );
            }
            else
            {
                continue; // どちらの座標もなければスキップ
            }

            GameObject starObj = starPrefab != null ? Instantiate(starPrefab, position, Quaternion.identity) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            starObj.transform.position = position;
            starObj.transform.SetParent(starContainer.transform);
            starObj.name = !string.IsNullOrEmpty(starData.properName) ? starData.properName : $"Star_{createdCount}";

            float scale = Mathf.Clamp((magnitudeBaseValue - starData.magnitude) * magnitudeScaleFactor, minMaxScale.x, minMaxScale.y);
            starObj.transform.localScale = Vector3.one * scale;

            var renderer = starObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var starMaterial = new Material(Shader.Find("Standard"));
                Color starColor = GetColorFromData(starData);
                starMaterial.color = starColor;
                starMaterial.EnableKeyword("_EMISSION");
                starMaterial.SetColor("_EmissionColor", starColor * emissionIntensity);
                renderer.material = starMaterial;
            }

            var starInfo = starObj.AddComponent<StarInfo>();
            starInfo.data = starData;
            createdCount++;
        }
        Debug.Log($"{createdCount}個の星オブジェクトの生成が完了しました。");
    }

    #region Helper Methods
    private float GetFloatValue(string[] values, string columnName, float defaultValue = 0f)
    {
        if (headerMap.TryGetValue(columnName.ToLower(), out int index) && values.Length > index)
        {
            if (float.TryParse(values[index], out float result)) return result;
        }
        return defaultValue;
    }

    private float? GetNullableFloatValue(string[] values, string columnName)
    {
        if (headerMap.TryGetValue(columnName.ToLower(), out int index) && values.Length > index)
        {
            if (float.TryParse(values[index], out float result)) return result;
        }
        return null;
    }

    private string GetStringValue(string[] values, string columnName)
    {
        if (headerMap.TryGetValue(columnName.ToLower(), out int index) && values.Length > index)
        {
            return values[index].Trim();
        }
        return "";
    }

    private Color GetColorFromData(StarData data)
    {
        if (data.colorIndex.HasValue) return GetColorFromColorIndex(data.colorIndex.Value);
        if (!string.IsNullOrEmpty(data.spectralType)) return GetColorFromSpectralType(data.spectralType);
        return Color.white;
    }

    private Color GetColorFromColorIndex(float ci)
    {
        if (ci < -0.33f) return new Color(0.67f, 0.76f, 1.00f);
        if (ci < 0.00f) return new Color(0.78f, 0.84f, 1.00f);
        if (ci < 0.30f) return Color.white;
        if (ci < 0.58f) return new Color(1.00f, 0.98f, 0.83f);
        if (ci < 0.81f) return new Color(1.00f, 0.91f, 0.71f);
        if (ci < 1.40f) return new Color(1.00f, 0.78f, 0.51f);
        return new Color(1.00f, 0.67f, 0.45f);
    }

    private Color GetColorFromSpectralType(string spectralType)
    {
        if (string.IsNullOrEmpty(spectralType)) return Color.white;
        char typeChar = spectralType.Trim().ToUpper()[0];
        switch (typeChar)
        {
            case 'O': return new Color(0.67f, 0.76f, 1.00f);
            case 'B': return new Color(0.78f, 0.84f, 1.00f);
            case 'A': return Color.white;
            case 'F': return new Color(1.00f, 0.98f, 0.83f);
            case 'G': return new Color(1.00f, 0.91f, 0.71f);
            case 'K': return new Color(1.00f, 0.78f, 0.51f);
            case 'M': return new Color(1.00f, 0.67f, 0.45f);
            default: return Color.gray;
        }
    }
    #endregion
}

public class StarInfo : MonoBehaviour
{
    public csvdata.StarData data;
}