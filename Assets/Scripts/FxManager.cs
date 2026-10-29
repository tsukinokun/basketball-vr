using System.Collections;
using UnityEngine;
using UnityEngine.UI; //uGUIの利用に必要

//ゴール・コンボ・時間切れなどの派手な演出をまとめて担当する（GameManagerと同じオブジェクトに付ける）
//VR酔いを避けるため、カメラは揺らさずに周りを光らせる・飛ばす演出だけにしている
public class FxManager : MonoBehaviour
{
    [SerializeField] Material GlowMat; //加算合成で光る素材（パーティクル・軌跡に使う）
    [SerializeField] Transform HoopCenter; //リングの中心（開始・時間切れの演出位置）
    [SerializeField] Transform[] FlashRoots; //ゴール時に光らせるもの（リム・ネット）
    [SerializeField] Color FlashColor = new Color(1.0f, 0.85f, 0.2f); //光らせる色
    [SerializeField] float FlashTime = 0.6f; //光が消えるまでの時間
    [SerializeField] RectTransform ScoreText; //ゴール時に弾ませるスコア表示
    [SerializeField] Text TimeText; //残り時間表示
    [SerializeField] float HurryTime = 10.0f; //残りがこれ以下になったら時間表示を赤く脈打たせる
    [SerializeField] int FireCombo = 3; //この連続ゴール数からボールが燃える
    [SerializeField] int ConfettiAmount = 120; //紙吹雪の量（コンボで増える）

    int Combo; //連続ゴール数（外したら0に戻る）
    Renderer[] FlashRenderers; //光らせるレンダラー
    Color[] FlashBaseColors; //光らせる前の色
    Coroutine FlashRoutine;
    Coroutine PunchRoutine;
    Vector3 ScoreBaseScale;
    Vector3 TimeBaseScale;
    Color TimeBaseColor;
    Gradient Rainbow; //紙吹雪の色
    Gradient FireColor; //炎の色

    bool OnFire { get { return Combo >= FireCombo; } }

    void Awake()
    {
        //光らせるレンダラーと元の色を覚えておく
        System.Collections.Generic.List<Renderer> List = new System.Collections.Generic.List<Renderer>();
        foreach (Transform Root in FlashRoots)
        {
            if (Root != null) List.AddRange(Root.GetComponentsInChildren<Renderer>());
        }
        FlashRenderers = List.ToArray();
        FlashBaseColors = new Color[FlashRenderers.Length];
        for (int i = 0; i < FlashRenderers.Length; i++)
        {
            FlashBaseColors[i] = FlashRenderers[i].material.color;
        }
        if (ScoreText != null) ScoreBaseScale = ScoreText.localScale;
        if (TimeText != null)
        {
            TimeBaseScale = TimeText.rectTransform.localScale;
            TimeBaseColor = TimeText.color;
        }

        Rainbow = new Gradient();
        Rainbow.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1.0f, 0.2f, 0.3f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.8f, 0.1f), 0.25f),
                new GradientColorKey(new Color(0.2f, 1.0f, 0.4f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0.6f, 1.0f), 0.75f),
                new GradientColorKey(new Color(0.9f, 0.3f, 1.0f), 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) });
        FireColor = new Gradient();
        FireColor.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1.0f, 0.95f, 0.5f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.45f, 0.05f), 0.4f),
                new GradientColorKey(new Color(0.8f, 0.05f, 0.0f), 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
    }

    //プレイ開始 **********************************************
    public void StartFx()
    {
        Combo = 0;
        Vector3 Pos = HoopCenter != null ? HoopCenter.position : transform.position;
        Popup(Pos + Vector3.up * 0.5f, "GO!!", new Color(0.3f, 1.0f, 0.5f), 1.6f);
        Burst(Pos, 80, 4.0f, 0.1f, 1.2f, 0.3f, Rainbow, true);
    }

    //ゴール **********************************************
    public void GoalFx(Vector3 Pos, int Point)
    {
        Combo++;
        float Power = Mathf.Min(1.0f + (Combo - 1) * 0.35f, 3.0f); //コンボが続くほど派手に

        //紙吹雪・衝撃波リング・吹き上がる火花
        Burst(Pos, Mathf.RoundToInt(ConfettiAmount * Power), 4.0f * Power, 0.09f, 2.2f, 0.5f, Rainbow, true);
        Ring(Pos, Power);
        if (OnFire) Burst(Pos, 80, 6.0f, 0.15f, 0.8f, -0.2f, FireColor, false);

        Flash();
        PunchScore(Power);

        //得点とコンボのポップアップ
        Popup(Pos + Vector3.up * 0.4f, "+" + Point, Color.white, 1.0f);
        string Msg;
        Color MsgColor;
        if (OnFire) { Msg = "ON FIRE!!  x" + Combo; MsgColor = new Color(1.0f, 0.45f, 0.1f); }
        else if (Combo >= 2) { Msg = "COMBO x" + Combo; MsgColor = new Color(0.3f, 0.9f, 1.0f); }
        else { Msg = "NICE!"; MsgColor = new Color(1.0f, 0.9f, 0.2f); }
        Popup(Pos + Vector3.up * 1.0f, Msg, MsgColor, 1.2f + 0.15f * Mathf.Min(Combo, 5));
    }

    //外した **********************************************
    public void MissFx()
    {
        Combo = 0;
    }

    //時間切れ **********************************************
    public void TimeUpFx(int Score)
    {
        Combo = 0;
        Vector3 Pos = HoopCenter != null ? HoopCenter.position : transform.position;
        Popup(Pos + Vector3.up * 1.1f, "TIME UP!", new Color(1.0f, 0.3f, 0.3f), 1.8f);
        Popup(Pos + Vector3.up * 0.3f, "SCORE " + Score, Color.white, 1.4f);
        Burst(Pos, 300, 5.0f, 0.1f, 3.0f, 0.4f, Rainbow, true);
        if (TimeText != null)
        {
            TimeText.rectTransform.localScale = TimeBaseScale;
            TimeText.color = TimeBaseColor;
        }
    }

    //残り時間表示（残りわずかなら1秒ごとに赤く脈打つ） **********************************************
    public void UpdateTime(float Remain)
    {
        if (TimeText == null) return;
        if (Remain > HurryTime)
        {
            TimeText.rectTransform.localScale = TimeBaseScale;
            TimeText.color = TimeBaseColor;
            return;
        }
        float Beat = Remain - Mathf.Floor(Remain); //秒が切り替わる瞬間に1、そこから0へ
        TimeText.rectTransform.localScale = TimeBaseScale * (1.0f + 0.3f * Beat * Beat);
        TimeText.color = Color.Lerp(TimeBaseColor, new Color(1.0f, 0.15f, 0.1f), 0.5f + 0.5f * Beat);
    }

    //ボールに軌跡を付ける（燃えている時は太く長い炎の軌跡と火の粉） **********************************************
    public void AttachTrail(GameObject Ball)
    {
        TrailRenderer Tr = Ball.AddComponent<TrailRenderer>();
        Tr.material = GlowMat;
        Tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Tr.minVertexDistance = 0.02f;
        Tr.emitting = false; //投げた時から出す（BallAction側で切り替える）
        if (OnFire)
        {
            Tr.time = 0.5f;
            Tr.widthMultiplier = 0.28f;
            Tr.colorGradient = FireColor;
            ParticleSystem Fire = MakeParticles(Ball.transform.position, FireColor, true);
            Fire.transform.SetParent(Ball.transform, true);
            ParticleSystem.MainModule Main = Fire.main;
            Main.loop = true; //ボールが消えるまで燃え続ける
            Main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            Main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            Main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            Main.gravityModifier = -0.3f; //火の粉は上に昇る
            ParticleSystem.EmissionModule Emi = Fire.emission;
            Emi.rateOverTime = 70.0f;
            ParticleSystem.ShapeModule Shape = Fire.shape;
            Shape.radius = 0.1f;
            Fire.Play();
        }
        else
        {
            Gradient G = new Gradient();
            G.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1.0f, 0.8f, 0.4f), 0.0f), new GradientColorKey(new Color(1.0f, 0.4f, 0.1f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
            Tr.time = 0.25f;
            Tr.widthMultiplier = 0.15f;
            Tr.colorGradient = G;
        }
        Tr.widthCurve = AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f); //先から細くなる
    }

    //一瞬だけ弾け飛ぶパーティクル **********************************************
    void Burst(Vector3 Pos, int Count, float Speed, float Size, float Life, float Gravity, Gradient Colors, bool RandomColor)
    {
        ParticleSystem PS = MakeParticles(Pos, Colors, RandomColor);
        ParticleSystem.MainModule Main = PS.main;
        Main.startLifetime = new ParticleSystem.MinMaxCurve(Life * 0.6f, Life);
        Main.startSpeed = new ParticleSystem.MinMaxCurve(Speed * 0.4f, Speed);
        Main.startSize = new ParticleSystem.MinMaxCurve(Size * 0.6f, Size * 1.4f);
        Main.gravityModifier = Gravity;
        ParticleSystem.EmissionModule Emi = PS.emission;
        Emi.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, (short)Count) });
        ParticleSystem.ShapeModule Shape = PS.shape;
        Shape.shapeType = ParticleSystemShapeType.Sphere;
        Shape.radius = 0.15f;
        //空気抵抗でふわっと舞う
        ParticleSystem.LimitVelocityOverLifetimeModule Limit = PS.limitVelocityOverLifetime;
        Limit.enabled = true;
        Limit.limit = 100.0f;
        Limit.drag = 2.0f;
        PS.Play();
    }

    //リングの高さで横に広がる衝撃波 **********************************************
    void Ring(Vector3 Pos, float Power)
    {
        Gradient Gold = new Gradient();
        Gold.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(FlashColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) });
        ParticleSystem PS = MakeParticles(Pos, Gold, false);
        ParticleSystem.MainModule Main = PS.main;
        Main.startLifetime = 0.45f;
        Main.startSpeed = 3.0f * Power;
        Main.startSize = 0.18f;
        ParticleSystem.EmissionModule Emi = PS.emission;
        Emi.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, (short)(60 * Power)) });
        ParticleSystem.ShapeModule Shape = PS.shape;
        Shape.shapeType = ParticleSystemShapeType.Circle;
        Shape.radius = 0.25f; //リングの半径くらいから外へ
        Shape.radiusThickness = 0.0f; //円周から出す
        Shape.rotation = new Vector3(90.0f, 0.0f, 0.0f); //水平に寝かせる
        PS.Play();
    }

    //パーティクルを用意する共通処理（終わったら自動で消える） **********************************************
    ParticleSystem MakeParticles(Vector3 Pos, Gradient Colors, bool RandomColor)
    {
        GameObject Go = new GameObject("Fx");
        Go.transform.position = Pos;
        ParticleSystem PS = Go.AddComponent<ParticleSystem>();
        PS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); //設定を変える前に止める
        ParticleSystem.MainModule Main = PS.main;
        Main.duration = 0.2f;
        Main.loop = false;
        Main.simulationSpace = ParticleSystemSimulationSpace.World;
        Main.stopAction = ParticleSystemStopAction.Destroy;
        Main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
        ParticleSystem.MinMaxGradient StartColor = new ParticleSystem.MinMaxGradient(Colors);
        StartColor.mode = RandomColor ? ParticleSystemGradientMode.RandomColor : ParticleSystemGradientMode.Gradient;
        Main.startColor = RandomColor ? StartColor : new ParticleSystem.MinMaxGradient(Colors.Evaluate(0.0f));
        ParticleSystem.EmissionModule Emi = PS.emission;
        Emi.rateOverTime = 0.0f;
        //だんだん消える（ランダム色でない時はグラデーション通りに色も変わる）
        ParticleSystem.ColorOverLifetimeModule Col = PS.colorOverLifetime;
        Col.enabled = true;
        Gradient Fade = new Gradient();
        Fade.SetKeys(
            RandomColor ? new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) } : Colors.colorKeys,
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) });
        Col.color = Fade;
        ParticleSystemRenderer R = Go.GetComponent<ParticleSystemRenderer>();
        R.material = GlowMat;
        R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return PS;
    }

    //リム・ネットを光らせる **********************************************
    void Flash()
    {
        if (FlashRoutine != null) StopCoroutine(FlashRoutine);
        FlashRoutine = StartCoroutine(FlashRun());
    }

    IEnumerator FlashRun()
    {
        for (float t = 0.0f; t < FlashTime; t += Time.deltaTime)
        {
            float Rate = 1.0f - t / FlashTime;
            for (int i = 0; i < FlashRenderers.Length; i++)
            {
                FlashRenderers[i].material.color = Color.Lerp(FlashBaseColors[i], FlashColor, Rate * Rate);
            }
            yield return null;
        }
        for (int i = 0; i < FlashRenderers.Length; i++)
        {
            FlashRenderers[i].material.color = FlashBaseColors[i];
        }
    }

    //スコア表示を弾ませる **********************************************
    void PunchScore(float Power)
    {
        if (ScoreText == null) return;
        if (PunchRoutine != null) StopCoroutine(PunchRoutine);
        PunchRoutine = StartCoroutine(PunchRun(Power));
    }

    IEnumerator PunchRun(float Power)
    {
        const float PunchTime = 0.35f;
        for (float t = 0.0f; t < PunchTime; t += Time.deltaTime)
        {
            float Rate = 1.0f - t / PunchTime;
            ScoreText.localScale = ScoreBaseScale * (1.0f + 0.4f * Power * Rate * Rate);
            yield return null;
        }
        ScoreText.localScale = ScoreBaseScale;
    }

    //空中に浮かぶ文字（ポンと出て上に昇りながら消える） **********************************************
    void Popup(Vector3 Pos, string Msg, Color MsgColor, float Scale)
    {
        GameObject Go = new GameObject("Popup", typeof(RectTransform), typeof(Canvas));
        Go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform RT = (RectTransform)Go.transform;
        RT.sizeDelta = new Vector2(800.0f, 200.0f);
        RT.position = Pos;

        GameObject TxGo = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        TxGo.transform.SetParent(Go.transform, false);
        RectTransform TRT = (RectTransform)TxGo.transform;
        TRT.anchorMin = Vector2.zero;
        TRT.anchorMax = Vector2.one;
        TRT.offsetMin = Vector2.zero;
        TRT.offsetMax = Vector2.zero;
        Text Tx = TxGo.GetComponent<Text>();
        Tx.font = TimeText != null ? TimeText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Tx.fontSize = 120;
        Tx.fontStyle = FontStyle.Bold;
        Tx.alignment = TextAnchor.MiddleCenter;
        Tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        Tx.verticalOverflow = VerticalWrapMode.Overflow;
        Tx.color = MsgColor;
        Tx.text = Msg;
        Outline Ol = TxGo.GetComponent<Outline>();
        Ol.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.8f);
        Ol.effectDistance = new Vector2(4.0f, -4.0f);

        StartCoroutine(PopupRun(RT, Tx, Ol, Scale * 0.004f));
    }

    IEnumerator PopupRun(RectTransform RT, Text Tx, Outline Ol, float Scale)
    {
        const float Life = 1.3f;
        Vector3 Start = RT.position;
        Color BaseColor = Tx.color;
        Color OutlineColor = Ol.effectColor;
        for (float t = 0.0f; t < Life; t += Time.deltaTime)
        {
            if (RT == null) yield break;
            float Rate = t / Life;
            //最初の0.2秒で大きく弾んでから落ち着く
            float Pop = t < 0.2f ? Mathf.Sin(t / 0.2f * Mathf.PI * 0.5f) * 1.3f : Mathf.Lerp(1.3f, 1.0f, Mathf.Clamp01((t - 0.2f) / 0.15f));
            RT.localScale = Vector3.one * Scale * Pop;
            RT.position = Start + Vector3.up * (0.4f * Rate);
            Camera Cam = Camera.main;
            if (Cam != null) RT.rotation = Quaternion.LookRotation(RT.position - Cam.transform.position); //いつもこちらを向く
            float Alpha = Rate < 0.6f ? 1.0f : 1.0f - (Rate - 0.6f) / 0.4f;
            Tx.color = new Color(BaseColor.r, BaseColor.g, BaseColor.b, BaseColor.a * Alpha);
            Ol.effectColor = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, OutlineColor.a * Alpha);
            yield return null;
        }
        Destroy(RT.gameObject);
    }
}
