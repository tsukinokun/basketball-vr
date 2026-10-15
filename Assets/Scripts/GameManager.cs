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
    [SerializeField] Vector3 SpawnPosVR = new Vector3(0.2f, 0.9f, 0.4f); //ボール出現位置（VR）
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        myAudio = GetComponent<AudioSource>(); //自身の音源を取得
        Ready(); //初期化処理
    }

    //初期化処理 ***************************************************
    void Ready()
    {
        GameMode = MODE.TITLE;
        Score = 0;
        Elapsed = 0.0f;
        Waited = 0.0f;
        txtScore.text = "Score : " + Score.ToString("D3");
        txtTime.text = "Time : " + LimitTime.ToString("00.00") + "s";
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
        Score += GoalPoint;
        txtScore.text = "Score : " + Score.ToString("D3"); //スコア表示
        myAudio.PlayOneShot(SE_Goal); //ゴール音

        //コインエフェクトを生成
        GameObject Fx = Instantiate(PatCoin, GoalPos, Quaternion.identity);
        ParticleSystem.EmissionModule Emi =
        Fx.GetComponent<ParticleSystem>().emission;
        Emi.rateOverTime = PatAmount;
        Destroy(Fx, PatCoinLife); //コインエフェクトの撤去予約
    }

    //ボール生成処理 ***********************************************
    void SpawnBall()
    {
        Vector3 Pos = VRAction.isVR ? SpawnPosVR : SpawnPosPC;
        GameObject B = Instantiate(BallPrefab, Pos, Quaternion.identity);
        B.SendMessage("SetManager", gameObject,
        SendMessageOptions.DontRequireReceiver); //マネージャーを教える
        WaitingBall = B.GetComponent<BallAction>();
    }

    //タイムアップ処理 ***********************************************
    void TimeUp()
    {
        GameMode = MODE.TIMEUP;
        Elapsed = 0.0f;
        Waited = 0.0f;
        txtTime.text = "Time : 00.00s";
        myAudio.Stop(); //BGM停止
        myAudio.PlayOneShot(SE_TimeUp); //タイムアップ音
        GameObject[] Items = GameObject.FindGameObjectsWithTag("Item");
        foreach (GameObject Stored in Items)
        {
            Destroy(Stored); //残ったボールを全撤去
        }
        WaitingBall = null;
    }
    //プレイ開始処理 ***********************************************
    void PlayStart()
    {
        GameMode = MODE.PLAYING;
        Elapsed = 0.0f;
        Waited = 0.0f;
        txtMessage.text = "";
        myAudio.Play(); //BGM再生
    }

    // Update is called once per frame
    void Update()
    {
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
                    txtTime.text = "Time : " + (LimitTime - Elapsed).ToString("00.00") + "s";
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
