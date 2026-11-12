using System.Collections;
using UnityEngine;
using UnityEngine.UI; //uGUIの利用に必要

//アリーナ（観客席・リボンボード・スコアボード）の演出と、観客の音を担当する
//ArenaBuilder（Tools > Basketball > Build Arena）で作ったアリーナのルートに付く
public class ArenaFx : MonoBehaviour
{
    public static ArenaFx Instance { get; private set; } //ボールやFxManagerから呼ぶ用

    [Header("観客")]
    [SerializeField] Transform[] CrowdSections; //ブロックごとの観客（盛り上がると跳ねる）
    [SerializeField] float JumpHeight = 0.2f; //一番盛り上がった時に跳ねる高さ（m）
    [SerializeField] float HypeDecay = 0.3f; //盛り上がりが1秒あたりに冷める量

    [Header("リボンボード・スコアボード")]
    [SerializeField] Renderer[] Ribbons; //流れるLEDボード
    [SerializeField] float RibbonSpeed = 0.12f; //LEDの流れる速さ
    [SerializeField] Graphic[] ScreenBacks; //ゴール時に光らせるスコアボードの背景

    [Header("カメラのフラッシュ")]
    [SerializeField] Material GlowMat; //加算合成で光る素材
    [SerializeField] Bounds[] FlashAreas; //観客席の範囲（ワールド座標）

    [Header("音")]
    [SerializeField] AudioClip AMB_Crowd; //観客のざわめき（ループ）
    [SerializeField] AudioClip SE_Cheer; //歓声
    [SerializeField] AudioClip SE_Applause; //拍手
    [SerializeField] AudioClip SE_Whistle; //ホイッスル
    [SerializeField] float AmbientIdle = 0.25f; //タイトル中のざわめきの音量
    [SerializeField] float AmbientPlay = 0.4f; //プレイ中のざわめきの音量

    AudioSource Ambient; //ざわめき用
    AudioSource Voice; //歓声・拍手・ホイッスル用
    float AmbientTarget;
    Vector3[] SectionBase; //観客ブロックの元の位置
    float[] SectionPhase; //ブロックごとに跳ねるタイミングをずらす
    float Hype; //盛り上がり（0〜1）
    bool Fire; //連続ゴールで燃えているか
    float FlashRate; //リボンボードが光っている強さ（1→0）
    Material RibbonMat; //リボンボードの素材（実行時の複製）
    Color RibbonBase;
    Color ScreenBase;
    ParticleSystem Flashes; //カメラのフラッシュ
    ParticleSystem Dust; //床でボールが弾んだ時の輪

    void Awake()
    {
        Instance = this;
        SectionBase = new Vector3[CrowdSections.Length];
        SectionPhase = new float[CrowdSections.Length];
        for (int i = 0; i < CrowdSections.Length; i++)
        {
            SectionBase[i] = CrowdSections[i].localPosition;
            SectionPhase[i] = Random.Range(0.0f, Mathf.PI * 2.0f);
        }
        //リボンボードは全部で1つの素材を共有して、まとめて色を変える
        if (Ribbons.Length > 0 && Ribbons[0] != null)
        {
            RibbonMat = new Material(Ribbons[0].sharedMaterial);
            RibbonBase = RibbonMat.GetColor("_BaseColor");
            foreach (Renderer R in Ribbons)
            {
                if (R != null) R.sharedMaterial = RibbonMat;
            }
        }
        if (ScreenBacks.Length > 0 && ScreenBacks[0] != null) ScreenBase = ScreenBacks[0].color;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (RibbonMat != null) Destroy(RibbonMat);
    }

    void Start()
    {
        Ambient = gameObject.AddComponent<AudioSource>();
        Ambient.clip = AMB_Crowd;
        Ambient.loop = true;
        Ambient.playOnAwake = false;
        Ambient.spatialBlend = 0.0f; //会場全体から聞こえる
        AmbientTarget = AmbientIdle;
        Ambient.volume = AmbientIdle;
        if (AMB_Crowd != null) Ambient.Play();

        Voice = gameObject.AddComponent<AudioSource>();
        Voice.playOnAwake = false;
        Voice.spatialBlend = 0.0f;

        Flashes = MakeParticles("CameraFlashes", 600);
        Dust = MakeParticles("BounceDust", 200);
    }

    //プレイ開始（ホイッスル） **********************************************
    public void StartGame()
    {
        Voice.pitch = 1.0f;
        if (SE_Whistle != null) Voice.PlayOneShot(SE_Whistle, 0.8f);
        AmbientTarget = AmbientPlay;
        Hype = Mathf.Max(Hype, 0.4f);
        Fire = false;
    }

    //ゴール（Power は 1〜3、コンボが続くほど大きい） **********************************************
    public void Goal(float Power, bool OnFire)
    {
        Hype = Mathf.Clamp01(0.5f + 0.17f * Power);
        Fire = OnFire;
        FlashRate = 1.0f;
        if (SE_Cheer != null)
        {
            Voice.pitch = Random.Range(0.95f, 1.06f);
            Voice.PlayOneShot(SE_Cheer, Mathf.Clamp01(0.45f + 0.18f * Power));
        }
        StartCoroutine(FlashRun(Mathf.RoundToInt(25 * Power), 1.2f));
    }

    //外した（燃えている状態が終わる） **********************************************
    public void Miss()
    {
        Fire = false;
    }

    //時間切れ（拍手と大歓声） **********************************************
    public void TimeUp()
    {
        Voice.pitch = 1.0f;
        if (SE_Applause != null) Voice.PlayOneShot(SE_Applause, 0.9f);
        if (SE_Cheer != null) Voice.PlayOneShot(SE_Cheer, 0.7f);
        Hype = 1.0f;
        Fire = false;
        FlashRate = 1.0f;
        AmbientTarget = AmbientIdle;
        StartCoroutine(FlashRun(160, 3.0f));
    }

    //床で強く弾んだ所に小さな輪を出す **********************************************
    public void Impact(Vector3 Pos, float Speed)
    {
        if (Dust == null || Speed < 2.5f) return;
        float Rate = Mathf.Clamp01((Speed - 2.5f) / 6.0f);
        int Count = 10 + Mathf.RoundToInt(10 * Rate);
        ParticleSystem.EmitParams P = new ParticleSystem.EmitParams();
        for (int i = 0; i < Count; i++)
        {
            float A = (i + Random.value * 0.5f) / Count * Mathf.PI * 2.0f;
            Vector3 Dir = new Vector3(Mathf.Cos(A), 0.0f, Mathf.Sin(A));
            P.position = new Vector3(Pos.x, 0.02f, Pos.z) + Dir * 0.08f;
            P.velocity = Dir * (0.6f + 1.2f * Rate) + Vector3.up * 0.1f;
            P.startSize = 0.05f + 0.04f * Rate;
            P.startLifetime = 0.35f;
            P.startColor = new Color(1.0f, 0.9f, 0.7f, 0.35f + 0.3f * Rate);
            Dust.Emit(P, 1);
        }
    }

    void Update()
    {
        float Dt = Time.deltaTime;
        Hype = Mathf.Max(0.0f, Hype - HypeDecay * Dt);
        FlashRate = Mathf.Max(0.0f, FlashRate - Dt * 1.2f);

        //ざわめきは盛り上がりに合わせて大きくなる
        if (Ambient != null)
        {
            float Target = AmbientTarget + 0.25f * Hype;
            Ambient.volume = Mathf.MoveTowards(Ambient.volume, Target, Dt * 0.5f);
        }

        //観客がブロックごとにずれて跳ねる
        float T = Time.time;
        for (int i = 0; i < CrowdSections.Length; i++)
        {
            float Jump = JumpHeight * Hype * Mathf.Max(0.0f, Mathf.Sin(T * 9.0f + SectionPhase[i]));
            CrowdSections[i].localPosition = SectionBase[i] + Vector3.up * Jump;
        }

        //リボンボード：いつも流れていて、ゴールで虹色に光り、燃えている間はオレンジに脈打つ
        if (RibbonMat != null)
        {
            Vector2 Ofs = RibbonMat.GetTextureOffset("_BaseMap");
            Ofs.x = Mathf.Repeat(Ofs.x + RibbonSpeed * (1.0f + 3.0f * Hype) * Dt, 1.0f);
            RibbonMat.SetTextureOffset("_BaseMap", Ofs);
            Color C = RibbonBase;
            if (Fire) C = Color.Lerp(C, new Color(1.0f, 0.45f, 0.1f), 0.5f + 0.5f * Mathf.Sin(T * 8.0f));
            if (FlashRate > 0.0f) C = Color.Lerp(C, Color.HSVToRGB(Mathf.Repeat(T * 1.5f, 1.0f), 0.7f, 1.0f) * 1.4f, FlashRate);
            RibbonMat.SetColor("_BaseColor", C);
        }
        Color Screen = Color.Lerp(ScreenBase, new Color(1.0f, 0.75f, 0.2f), FlashRate * FlashRate * 0.6f);
        foreach (Graphic G in ScreenBacks)
        {
            if (G != null) G.color = Screen;
        }
    }

    //観客席のあちこちでカメラのフラッシュが光る **********************************************
    IEnumerator FlashRun(int Count, float Duration)
    {
        if (Flashes == null || FlashAreas.Length == 0) yield break;
        ParticleSystem.EmitParams P = new ParticleSystem.EmitParams();
        float Emitted = 0.0f;
        for (float t = 0.0f; t < Duration; t += Time.deltaTime)
        {
            //最初に多く、だんだん減っていく
            float Rate = 1.0f - t / Duration;
            Emitted += Count * 2.0f * Rate * Time.deltaTime / Duration;
            while (Emitted >= 1.0f)
            {
                Emitted -= 1.0f;
                Bounds B = FlashAreas[Random.Range(0, FlashAreas.Length)];
                P.position = new Vector3(
                    Random.Range(B.min.x, B.max.x),
                    Random.Range(B.min.y, B.max.y),
                    Random.Range(B.min.z, B.max.z));
                P.velocity = Vector3.zero;
                P.startSize = Random.Range(0.25f, 0.55f);
                P.startLifetime = Random.Range(0.06f, 0.14f);
                P.startColor = new Color(1.0f, 1.0f, 0.95f, 1.0f);
                Flashes.Emit(P, 1);
            }
            yield return null;
        }
    }

    //Emitで出すためのパーティクル（自分では出さない） **********************************************
    ParticleSystem MakeParticles(string Name, int Max)
    {
        GameObject Go = new GameObject(Name);
        Go.transform.SetParent(transform, false);
        ParticleSystem PS = Go.AddComponent<ParticleSystem>();
        PS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); //設定を変える前に止める
        ParticleSystem.MainModule Main = PS.main;
        Main.loop = true;
        Main.playOnAwake = false;
        Main.maxParticles = Max;
        Main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule Emi = PS.emission;
        Emi.rateOverTime = 0.0f;
        ParticleSystem.ShapeModule Shape = PS.shape;
        Shape.enabled = false;
        //だんだん消える
        ParticleSystem.ColorOverLifetimeModule Col = PS.colorOverLifetime;
        Col.enabled = true;
        Gradient Fade = new Gradient();
        Fade.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
        Col.color = Fade;
        ParticleSystemRenderer R = Go.GetComponent<ParticleSystemRenderer>();
        R.material = GlowMat;
        R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        R.receiveShadows = false;
        PS.Play();
        return PS;
    }
}
