using System.Collections;  // ArrayList, Hashtable, Queue, Stack, BitArrayなど、非ジェネリックコレクション。格納できる要素がobjectで統一
using System.Collections.Generic;  // List<>, Dictionary<Key, Value> ジェネリック、型安全
using UnityEngine;  // Unity独自の機能やクラス
using UnityEngine.Networking;  // Unityでネットワーク通信機能を利用する為のもの

public class csvReader : MonoBehaviour
{
    // フィールド
    // [SerializeField]→インスペクターから編集可能
    [SerializeField] private string csvUrl = "https://hebbkx1anhila5yf.public.blob.vercel-storage.com/allsky-EBRS7U79vlOKRuLYLJkMivyTvgmKoO.csv";  // CSVのURLを格納
    [SerializeField] private GameObject starPrefab;  // Prefabを格納
    [SerializeField] private float sphereRadius = 100f;  // 球の半径、初期値は100(float型)

    private List<StarData> stars = new List<StarData>();  // 星のデータを複数纏めて管理する
    private UnityWebRequest webRequest;  // 外部のCSVやAPIからデータをダウンロードする際の変数、通信状況、結果を管理

    [System.Serializable]
    public class StarData  // 星のデータを纏める為のクラス
    {
        public float rightAscension;  // 赤経、天球上の経度
        public float declination;  // 赤緯、天球上の緯度
        public float magnitude;  // 等級、星の明るさ
    }

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(DownloadAndProcessCSV());
    }

    void OnDisable()  // GameObjectやコンポーネントが「無効化」された時(ex)) setActive(false)やシーン遷移)に自動的に呼び出される
    {
        if (webRequest != null)  // 通信中の場合
        {
            webRequest.Abort();  // そのリクエストを強制的に中断
        }
    }

    IEnumerator DownloadAndProcessCSV()  // Unityのコルーチン
    {
        Debug.Log("CSVファイルのダウンロードを開始します");

        webRequest = UnityWebRequest.Get(csvUrl);  // 指定したURLからCSVファイルを取得するリクエストを作成

        yield return webRequest.SendWebRequest();  // リクエストの送信と通信の完了まで待機

        while (!webRequest.isDone)  // ダウンロードが完了するまで
        {
            float progress = webRequest.downloadProgress;
            Debug.Log($"ダウンロード進捗: {progress * 100:F1}%");
            yield return null;
        }

        if (webRequest.result != UnityWebRequest.Result.Success)  // 通信が失敗した場合はエラーメッセージを出して終了
        {
            Debug.LogError("ダウンロードエラー: " + webRequest.error);
            yield break;
        }

        string csvText = webRequest.downloadHandler.text;  // CSVの中身を文字列として取得
        Debug.Log("CSVファイルのダウンロードに成功しました");

        ProcessCSVData(csvText);

        CreateStars();
    }

    void ProcessCSVData(string csvText)  // ダウンロードしたCSVテキストをパースして、各行から星データ（StarData）を生成し、リストに追加する処理
    {
        string[] lines = csvText.Split('\n');
        Debug.Log($"CSVファイルの行数: {lines.Length}");

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');
            if (values.Length >= 3)
            {
                StarData star = new StarData();

                if (float.TryParse(values[0], out float ra))
                    star.rightAscension = ra;
                else
                    continue;

                if (float.TryParse(values[1], out float dec))
                    star.declination = dec;
                else
                    continue;

                if (float.TryParse(values[2], out float mag))
                    star.magnitude = mag;
                else
                    continue;

                stars.Add(star);
            }
        }

        Debug.Log($"処理された星の数: {stars.Count}");
    }

    void CreateStars()
    {
        if (starPrefab == null)  // プレハブ未設定チェック
        {
            Debug.LogError("星のプレハブが設定されていません");
            return;
        }

        foreach (StarData star in stars)  // 星データ毎にループ
        {
            // 赤経・赤緯から3D座標に変換
            float raRadians = star.rightAscension * Mathf.Deg2Rad;
            float decRadians = star.declination * Mathf.Deg2Rad;

            float x = sphereRadius * Mathf.Cos(decRadians) * Mathf.Cos(raRadians);
            float z = sphereRadius * Mathf.Cos(decRadians) * Mathf.Sin(raRadians);
            float y = sphereRadius * Mathf.Sin(decRadians);

            // 星のオブジェクトを生成
            GameObject starObj = Instantiate(starPrefab, new Vector3(x, y, z), Quaternion.identity, transform);

            // 等級に基づいてサイズを調整
            float size = Mathf.Clamp(1.0f / (star.magnitude + 2.0f), 0.05f, 0.5f);
            starObj.transform.localScale = new Vector3(size, size, size);

            // 名前を設定
            starObj.name = $"Star_RA{star.rightAscension}_DEC{star.declination}_MAG{star.magnitude}";
        }
    }

    // Update is called once per frame
        void Update()
    {
        
    }
}
