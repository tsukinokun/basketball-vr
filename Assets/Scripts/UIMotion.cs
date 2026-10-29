using UnityEngine;
using UnityEngine.UI; //uGUIの利用に必要

//3D上のスコアボードを常にゆっくり動かす（Canvasに付ける）
//要素ごとに速さ・揺れ幅・タイミングをばらばらにして、機械的にそろった動きに見えないようにする
//VR酔いしないよう、動きは小さく遅くしている
public class UIMotion : MonoBehaviour
{
    [System.Serializable]
    public class Floater
    {
        public RectTransform Target; //動かすもの
        public float Move = 0.03f; //上下の揺れ幅（m）
        public float Tilt = 1.0f; //傾きの揺れ幅（度）
        public float Speed = 1.0f; //揺れの速さ
        public float Phase = 0.0f; //揺れの始まりのずれ
        [HideInInspector] public Vector2 BasePos;
        [HideInInspector] public float BaseRot;
    }

    [System.Serializable]
    public class Spinner
    {
        public RectTransform Target; //回すもの
        public float Speed = 30.0f; //回る速さ（度/秒、マイナスで逆回り）
    }

    [SerializeField] Floater[] Floaters; //ふわふわ揺れるもの
    [SerializeField] Spinner[] Spinners; //回り続ける飾り
    [SerializeField] Graphic[] Pulses; //明るさが脈打つ飾り
    [SerializeField] float PulseSpeed = 1.7f;
    [SerializeField] RectTransform Shine; //タイトルを横切る光
    [SerializeField] float ShineFrom = -1.8f; //光の通り始め（m）
    [SerializeField] float ShineTo = 1.8f; //光の通り終わり（m）
    [SerializeField] float ShineTime = 0.7f; //光が横切る時間
    [SerializeField] float ShineInterval = 3.3f; //光が通る間隔
    [SerializeField] RawImage Stripes; //背景で流れ続ける斜めの縞
    [SerializeField] float StripeSpeed = 0.12f;
    [SerializeField] Text Message; //点滅するメッセージ
    [SerializeField] CanvasGroup MessagePlate; //メッセージの帯（メッセージが無い時は隠す）
    [SerializeField] float SwayAngle = 0.5f; //ボード全体の揺れ（度）
    [SerializeField] float SwayMove = 0.012f; //ボード全体の上下の揺れ（m）

    Vector3 BoardPos;
    Quaternion BoardRot;
    Color[] PulseBase;
    float MessageShown = -10.0f; //最後にメッセージが出ていた時刻

    void Awake()
    {
        BoardPos = transform.position;
        BoardRot = transform.rotation;
        foreach (Floater F in Floaters)
        {
            if (F.Target == null) continue;
            F.BasePos = F.Target.anchoredPosition;
            F.BaseRot = F.Target.localEulerAngles.z;
        }
        PulseBase = new Color[Pulses.Length];
        for (int i = 0; i < Pulses.Length; i++) PulseBase[i] = Pulses[i].color;
        if (Stripes != null) Stripes.texture = MakeStripes();
    }

    void Update()
    {
        float T = Time.time;

        //ボード全体：速さの違う2つの揺れを重ねて、周期が見えないようにする
        float SwayA = Mathf.Sin(T * 0.37f) + 0.5f * Mathf.Sin(T * 0.91f + 1.3f);
        float SwayB = Mathf.Sin(T * 0.53f + 0.7f);
        transform.position = BoardPos + Vector3.up * (SwayMove * SwayB);
        transform.rotation = BoardRot * Quaternion.Euler(SwayAngle * 0.6f * SwayB, SwayAngle * SwayA, 0.0f);

        foreach (Floater F in Floaters)
        {
            if (F.Target == null) continue;
            float W = T * F.Speed + F.Phase;
            float Y = Mathf.Sin(W) + 0.35f * Mathf.Sin(W * 2.3f + 0.9f);
            F.Target.anchoredPosition = F.BasePos + new Vector2(0.0f, F.Move * Y);
            F.Target.localRotation = Quaternion.Euler(0.0f, 0.0f, F.BaseRot + F.Tilt * Mathf.Sin(W * 0.7f + 2.1f));
        }

        foreach (Spinner S in Spinners)
        {
            if (S.Target != null) S.Target.Rotate(0.0f, 0.0f, S.Speed * Time.deltaTime);
        }

        for (int i = 0; i < Pulses.Length; i++)
        {
            float Rate = 0.5f + 0.5f * Mathf.Sin(T * PulseSpeed * (1.0f + 0.23f * i) + i * 1.7f);
            Color C = PulseBase[i];
            Pulses[i].color = new Color(C.r, C.g, C.b, C.a * Mathf.Lerp(0.35f, 1.0f, Rate));
        }

        //タイトルの光：ShineInterval ごとに、ShineTime かけて左から右へ
        if (Shine != null)
        {
            float Rate = Mathf.Clamp01((T % ShineInterval) / ShineTime);
            Rate = Rate * Rate * (3.0f - 2.0f * Rate); //始めと終わりをなめらかに
            Vector2 P = Shine.anchoredPosition;
            Shine.anchoredPosition = new Vector2(Mathf.Lerp(ShineFrom, ShineTo, Rate), P.y);
        }

        if (Stripes != null)
        {
            Rect R = Stripes.uvRect;
            R.x = (T * StripeSpeed) % 1.0f;
            Stripes.uvRect = R;
        }

        //メッセージの帯：点滅で一瞬消えても帯は残し、しばらく出なければ隠す。出ている間は軽く脈打つ
        if (Message != null && MessagePlate != null)
        {
            if (Message.text != "") MessageShown = T;
            float Target = (T - MessageShown < 1.5f) ? 1.0f : 0.0f;
            MessagePlate.alpha = Mathf.MoveTowards(MessagePlate.alpha, Target, Time.deltaTime * 3.0f);
            float Beat = 1.0f + 0.03f * Mathf.Sin(T * 4.1f);
            MessagePlate.transform.localScale = new Vector3(Beat, Beat, 1.0f);
        }
    }

    //背景の斜めの縞模様を作る
    Texture2D MakeStripes()
    {
        const int Size = 64;
        Texture2D Tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Tex.wrapMode = TextureWrapMode.Repeat;
        Color32[] Px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                bool On = ((x + y) % 32) < 9;
                Px[y * Size + x] = On ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        Tex.SetPixels32(Px);
        Tex.Apply();
        return Tex;
    }
}
