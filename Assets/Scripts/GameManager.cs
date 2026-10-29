using UnityEngine;
using UnityEngine.UI; //uGUIの利用に必要
using UnityEngine.Serialization;
public class GameManager : MonoBehaviour
{
    [FormerlySerializedAs("SE_CrashCoins")]
    [SerializeField] AudioClip SE_Goal; //ゴール音
    [SerializeField] AudioClip SE_Tada;
    [SerializeField] AudioClip SE_Thrown;
    [SerializeField] AudioClip SE_TimeUp;
    AudioSource myAudio; //自身の音源
    FxManager Fx; //派手な演出の担当

    enum MODE
    {
        TITLE,
        PLAYING,
        TIMEUP
    }
    MODE GameMode; //ゲームの状態
    [SerializeField] Text txtMessage;
    [SerializeField] Text txtTime;
    [SerializeField] Text txtScore;
    [SerializeField] Image imgFill;
    [SerializeField] float LimitTime = 60.0f; //制限時間
    [SerializeField] float BlinkTime = 0.8f; //点滅時間
    [SerializeField] float BlinkInterval = 1.0f; //点滅間隔
    [SerializeField] float FillTime = 3.0f; //長押し時間
    float Elapsed; //経過時間
    float Waited; //待ち時間
    int Score; //獲得スコア

    [SerializeField] int GoalPoint = 2; //1ゴールの得点
    [SerializeField] Vector3 SpawnPosVR = new Vector3(0.2f, 0.9f, 0.4f); //ボール出現位置（VR）。高さは下の SpawnBelowEyeVR で目の高さから決める
    [SerializeField] float SpawnBelowEyeVR = 0.25f; //ボールを目の高さからこれだけ下（胸の上あたり）に出す（VR, m）
    [SerializeField] Vector3 SpawnPosPC = new Vector3(0.0f, 1.0f, 1.265f); //ボール出現位置（PC）
    [SerializeField] float SpawnInterval = 0.5f; //ボールを取ってから次が出るまでの時間
    [FormerlySerializedAs("PigPrefab")]
    [SerializeField] GameObject BallPrefab; //ボールプレハブ
    [SerializeField] GameObject PatCoin; //ゴール時のコインエフェクト
    [SerializeField] float PatCoinLife = 2.0f; //コインエフェクトの寿命
    [SerializeField] int PatAmount = 150; //コインエフェクトの量
    BallAction WaitingBall; //手に取られるのを待っているボール
    [SerializeField] OVRHand PinchHand; //素手で開始・タイトル戻りに使う手（左手のピンチ）
    bool isPinching; //今ピンチしているか
    bool wasPinching; //前フレームでピンチしていたか
    [SerializeField] float SeatedEyeHeight = 1.3f; //目の高さがこれより低ければ座って遊んでいるとみなす（VR, m）
    [SerializeField] float StandingEyeHeight = 1.6f; //座っている時に合わせる、立った時の目の高さ（VR, m）
    OVRCameraRig Rig; //カメラリグ（VR）
    bool HeightAdjusted = false; //高さ合わせが済んだか（false にすると次のフレームで測り直す）

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        myAudio = GetComponent<AudioSource>(); //自身の音源を取得
        Fx = GetComponent<FxManager>(); //演出担当を取得
        if (VRAction.isVR)
        {
            //VRは床を基準にトラッキングし、目の高さを実際の身長に合わせる（リング3.05mが本物と同じ高さに見える）
            //カメラリグの高さ1.3mはPCで遊ぶ時の目の高さなので、VRでは床に置く
            if (OVRManager.instance != null)
            {
                OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            }
            Rig = FindFirstObjectByType<OVRCameraRig>();
            if (Rig != null)
            {
                Vector3 RigPos = Rig.transform.position;
                Rig.transform.position = new Vector3(RigPos.x, 0.0f, RigPos.z);
            }
            if (OVRManager.display != null) OVRManager.display.RecenteredPose += OnRecentered;
        }
        Ready(); //初期化処理
    }

    //座って遊んでいる時は、目の高さが立った時の高さになるようにカメラリグを持ち上げる（VR）
    //立っている時は実際の身長のまま。測るのは起動時・プレイ開始時・リセンター（向き直し）時だけ
    //（遊んでいる途中に測り直すと、前かがみなどで高さが上下してしまう）
    void AdjustSeatedHeight()
    {
        if (HeightAdjusted || Rig == null || Rig.centerEyeAnchor == null || Time.time < 1.0f) return;
        float Eye = Rig.centerEyeAnchor.position.y - Rig.transform.position.y; //床から目までの高さ
        if (Eye < 0.5f) return; //まだトラッキングが始まっていない
        bool Seated = Eye < SeatedEyeHeight;
        Vector3 RigPos = Rig.transform.position;
        Rig.transform.position = new Vector3(RigPos.x, Seated ? StandingEyeHeight - Eye : 0.0f, RigPos.z);
        Debug.Log("[Height] eye=" + Eye.ToString("F2") + "m seated=" + Seated + " rig=" + Rig.transform.position.y.ToString("F2") + "m"); //実機での調整用
        HeightAdjusted = true;
    }

    //リセンターされたら高さを測り直す
    void OnRecentered()
    {
        HeightAdjusted = false;
    }

    void OnDestroy()
    {
        if (OVRManager.display != null) OVRManager.display.RecenteredPose -= OnRecentered;
    }

    //初期化処理 ***************************************************
    void Ready()
    {
        GameMode = MODE.TITLE;
        Score = 0;
        Elapsed = 0.0f;
        Waited = 0.0f;
        txtScore.text = Score.ToString("D3"); //「SCORE」の見出しはボード側にある
        txtTime.text = LimitTime.ToString("00.00");
        imgFill.fillAmount = 0.0f;
        myAudio.PlayOneShot(SE_Tada); //開始サウンド
    }

    //投げた時の処理 *********************************************
    void SoundThrown()
    {
        myAudio.PlayOneShot(SE_Thrown); //投げた音
    }

    //ゴール報告時 **********************************************
    void Goal(Vector3 GoalPos)
    {
        if (GameMode != MODE.PLAYING) return; //プレイ中でなければ無効
        Debug.Log("[Goal] score=" + (Score + GoalPoint)); //実機での調整用
        Score += GoalPoint;
        txtScore.text = Score.ToString("D3"); //スコア表示（「SCORE」の見出しはボード側にある）
        myAudio.PlayOneShot(SE_Goal); //ゴール音

        //コインエフェクトを生成
        GameObject CoinFx = Instantiate(PatCoin, GoalPos, Quaternion.identity);
        ParticleSystem.EmissionModule Emi =
        CoinFx.GetComponent<ParticleSystem>().emission;
        Emi.rateOverTime = PatAmount;
        Destroy(CoinFx, PatCoinLife); //コインエフェクトの撤去予約

        Fx.GoalFx(GoalPos, GoalPoint); //紙吹雪・光・ポップアップなどの派手な演出
    }

    //投げたボールが入らずに消えた報告 **********************************************
    void Missed()
    {
        if (GameMode != MODE.PLAYING) return;
        Fx.MissFx(); //連続ゴールが途切れる
    }

    //ボール生成処理 ***********************************************
    void SpawnBall()
    {
        Vector3 Pos = VRAction.isVR ? SpawnPosVR : SpawnPosPC;
        Transform Eye = (Rig != null && Rig.centerEyeAnchor != null) ? Rig.centerEyeAnchor
            : (Camera.main != null ? Camera.main.transform : null);
        if (VRAction.isVR && Eye != null)
        {
            Pos.y = Eye.position.y - SpawnBelowEyeVR; //身長に関係なく手に取りやすい高さに
            Debug.Log("[Spawn] eye=" + Eye.position.y.ToString("F2") + "m ball=" + Pos.y.ToString("F2") + "m"); //実機での調整用
        }
        GameObject B = Instantiate(BallPrefab, Pos, Quaternion.identity);
        B.SendMessage("SetManager", gameObject,
        SendMessageOptions.DontRequireReceiver); //マネージャーを教える
        WaitingBall = B.GetComponent<BallAction>();
        Fx.AttachTrail(B); //投げた時の軌跡（連続ゴール中は炎）
    }

    //タイムアップ処理 ***********************************************
    void TimeUp()
    {
        GameMode = MODE.TIMEUP;
        Elapsed = 0.0f;
        Waited = 0.0f;
        txtTime.text = "00.00";
        myAudio.Stop(); //BGM停止
        myAudio.PlayOneShot(SE_TimeUp); //タイムアップ音
        GameObject[] Items = GameObject.FindGameObjectsWithTag("Item");
        foreach (GameObject Stored in Items)
        {
            Destroy(Stored); //残ったボールを全撤去
        }
        WaitingBall = null;
        Fx.TimeUpFx(Score); //時間切れの演出
    }
    //プレイ開始処理 ***********************************************
    void PlayStart()
    {
        GameMode = MODE.PLAYING;
        Elapsed = 0.0f;
        Waited = 0.0f;
        HeightAdjusted = false; //プレイ開始時の姿勢で高さを合わせ直す
        txtMessage.text = "";
        myAudio.Play(); //BGM再生
        Fx.StartFx(); //開始の演出
    }

    // Update is called once per frame
    void Update()
    {
        AdjustSeatedHeight();
        Elapsed += Time.deltaTime;
        wasPinching = isPinching;
        isPinching = PinchHand != null && PinchHand.IsTracked &&
        PinchHand.GetFingerIsPinching(OVRHand.HandFinger.Index); //親指と人差し指をつまむ
        bool PinchDown = isPinching && !wasPinching;
        bool PinchUp = !isPinching && wasPinching;
        switch (GameMode)
        {
            case MODE.TITLE:
                Elapsed %= BlinkInterval; //メッセージ点滅
                txtMessage.text = (Elapsed < BlinkTime) ? "Pinch (L) to START" : "";
                if (Input.GetKeyDown(KeyCode.B) ||
                OVRInput.GetDown(OVRInput.RawButton.B) || PinchDown)
                {
                    PlayStart(); //プレイ開始処理
                }
                break;
            case MODE.PLAYING:
                //待機中のボールが取られたら、少し待って次のボールを出す
                if (WaitingBall == null || !WaitingBall.IsWaiting)
                {
                    Waited -= Time.deltaTime;
                    if (Waited <= 0.0f)
                    {
                        SpawnBall();
                        Waited = SpawnInterval;
                    }
                }
                imgFill.fillAmount = 1.0f - Elapsed / LimitTime; //残り時間

                if (LimitTime < Elapsed)
                {
                    TimeUp(); //タイムアップ処理
                }
                else
                {
                    txtTime.text = (LimitTime - Elapsed).ToString("00.00"); //「TIME」の見出しはボード側にある
                    Fx.UpdateTime(LimitTime - Elapsed); //残りわずかなら赤く脈打つ
                }
                break;
            case MODE.TIMEUP:
                Elapsed %= BlinkInterval; //メッセージ点滅
                txtMessage.text = (Elapsed < BlinkTime) ? "Pinch (L) 3 sec to TITLE" : "";
                if (Input.GetKey(KeyCode.A) ||
                OVRInput.Get(OVRInput.RawButton.A) || isPinching)
                {
                    Waited += Time.deltaTime;
                    if (Waited > FillTime)
                    {
                        Ready(); //初期化処理
                    }
                }
                if (Input.GetKeyUp(KeyCode.A) ||
                OVRInput.GetUp(OVRInput.RawButton.A) || PinchUp)
                {
                    Waited = 0.0f;
                }
                imgFill.fillAmount = Waited / FillTime;
                break;
            default:
                break;
        }
    }
}
