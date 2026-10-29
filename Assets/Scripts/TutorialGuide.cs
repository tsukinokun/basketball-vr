using UnityEngine;
using UnityEngine.UI; //uGUIの利用に必要

//毎ゲームの最初の1投が終わるまで、ボールと手の近くに投げ方の案内を3Dで出す（素手の時だけ）
//1. ボールに触る → 2. 頭の上に構える → 3. リングへ押し出して手首をスナップ
//タイマーは止めない。見た目はコードで作る（Managerに付ける）
public class TutorialGuide : MonoBehaviour
{
    [SerializeField] HandAction Hand; //ボールを持つ手（右手）
    [SerializeField] Transform Rim; //リング
    [SerializeField] Material GlowMat; //光る素材（弧の点線に使う）
    [SerializeField] Font CardFont; //文字のフォント
    [SerializeField] Sprite RingSprite; //目印のリングの画像
    [SerializeField] float CardScale = 0.0008f; //カードの大きさ（UIの1単位が何mか）。ボールは目の前0.5mほどに出るので小さめ
    [SerializeField] float ThrowSpeed = 1.5f; //離した時のボールがこれより速ければ投げたとみなす（m/s）
    [SerializeField] bool PreviewInEditor = false; //エディタでは素手が無くても案内を出す（見た目の確認用）
    [SerializeField] int PreviewStep = 0; //PreviewInEditor の時、1〜3 ならそのステップの見た目のまま止める（0 は普通に進む）

    enum STEP
    {
        OFF,
        GRAB, //ボールに触る
        RAISE, //頭の上に構える
        SHOOT, //リングへ投げる
        DONE //できた！
    }
    STEP Step = STEP.OFF;
    float StepTime; //今のステップになった時刻
    float NotReadyTime = -1.0f; //構えが崩れた時刻（-1 は崩れていない）
    BallAction WaitingBall; //出てきて待っているボール
    BallAction HeldBall; //チュートリアル中に持っているボール

    //見た目
    RectTransform Card;
    CanvasGroup CardGroup;
    Text StepText;
    Text MainText;
    Vector3 CardPos;
    RectTransform Marker; //矢印を並べる板
    RectTransform[] Chevrons = new RectTransform[3]; //流れる矢印
    Image[] ChevronImages;
    RectTransform RingMark; //目印のリング
    Image RingImage;
    LineRenderer Arc; //リングまでの弧
    Material ArcMat;

    static readonly Color Orange = new Color(1.0f, 0.42f, 0.08f);
    static readonly Color Navy = new Color(0.06f, 0.07f, 0.14f);

    void Awake()
    {
        BuildCard();
        BuildMarkers();
        BuildArc();
        HideAll();
    }

    //ゲーム開始 **********************************************
    public void Begin()
    {
        HeldBall = null;
        Go(STEP.GRAB);
    }

    //時間切れ **********************************************
    public void End()
    {
        Step = STEP.OFF;
        HideAll();
    }

    //新しいボールが出てきた **********************************************
    public void SetBall(BallAction Ball)
    {
        WaitingBall = Ball;
    }

    void Go(STEP Next)
    {
        Step = Next;
        StepTime = Time.time;
        NotReadyTime = -1.0f;
        switch (Next)
        {
            case STEP.GRAB: SetText("STEP 1/3", "TOUCH THE BALL"); break;
            case STEP.RAISE: SetText("STEP 2/3", "RAISE IT ABOVE\nYOUR HEAD"); break;
            case STEP.SHOOT: SetText("STEP 3/3", "PUSH TO THE RIM\n& FLICK YOUR WRIST"); break;
            case STEP.DONE: SetText("GOOD!", "NICE SHOT!"); break;
        }
    }

    void Update()
    {
        if (Step == STEP.OFF) return;
        bool Preview = PreviewInEditor && Application.isEditor;
        if (Hand == null || (!Hand.IsHandMode && !Preview))
        {
            HideAll(); //コントローラーの時は案内しない
            return;
        }
        if (Preview && PreviewStep >= 1 && PreviewStep <= 3)
        {
            STEP Want = (STEP)PreviewStep;
            if (Step != Want) Go(Want);
            if (HeldBall == null) HeldBall = WaitingBall;
            Show();
            return;
        }

        //ステップを進める
        switch (Step)
        {
            case STEP.GRAB:
                if (Hand.IsHolding && WaitingBall != null && !WaitingBall.IsWaiting)
                {
                    HeldBall = WaitingBall;
                    Go(STEP.RAISE);
                }
                break;
            case STEP.RAISE:
                if (!Hand.IsHolding || HeldBall == null) Released();
                else if (Hand.IsReadyToShoot) Go(STEP.SHOOT);
                break;
            case STEP.SHOOT:
                if (!Hand.IsHolding || HeldBall == null) Released();
                else if (!Hand.IsReadyToShoot)
                {
                    //少し下がっただけなら待つ。しばらく下げたままなら構えからやり直し
                    if (NotReadyTime < 0.0f) NotReadyTime = Time.time;
                    else if (Time.time - NotReadyTime > 0.6f) Go(STEP.RAISE);
                }
                else NotReadyTime = -1.0f;
                break;
            case STEP.DONE:
                if (Time.time - StepTime > 1.6f)
                {
                    Step = STEP.OFF;
                    HideAll();
                    return;
                }
                break;
        }
        if (Step == STEP.OFF) return;
        Show();
    }

    //持っていたボールを離した：投げたなら完了、落としただけならボールに触るところから
    void Released()
    {
        bool Thrown = false;
        if (HeldBall != null && HeldBall.IsThrown)
        {
            Rigidbody RB = HeldBall.GetComponent<Rigidbody>();
            Thrown = RB != null && RB.linearVelocity.magnitude > ThrowSpeed;
        }
        HeldBall = null;
        Go(Thrown ? STEP.DONE : STEP.GRAB);
    }

    //今のステップの見た目を出す **********************************************
    void Show()
    {
        Camera Cam = Camera.main;
        if (Cam == null) return;
        float T = Time.time;
        Vector3 CamPos = Cam.transform.position;
        Vector3 Right = Cam.transform.right;
        Vector3 HandPos = Hand.transform.position;
        Vector3 CardTarget = HandPos + Right * 0.28f + Vector3.up * 0.05f;
        bool ShowMarker = false, ShowRing = false, ShowArc = false;
        Vector3 From = Vector3.zero, To = Vector3.zero, RingPos = Vector3.zero;
        float RingSize = 0.3f;

        switch (Step)
        {
            case STEP.GRAB:
                if (WaitingBall == null) break;
                Vector3 BallPos = WaitingBall.transform.position;
                CardTarget = BallPos + Right * 0.3f + Vector3.up * 0.12f;
                From = BallPos + Vector3.up * 0.38f; //上から矢印がボールへ流れる
                To = BallPos + Vector3.up * 0.15f;
                ShowMarker = true;
                RingPos = BallPos;
                ShowRing = true;
                break;
            case STEP.RAISE:
                float GoalY = Mathf.Max(Hand.ShotReadyY + 0.08f, HandPos.y + 0.15f);
                From = HandPos + Vector3.up * 0.1f; //手から構える高さへ矢印が昇る
                To = new Vector3(HandPos.x, GoalY, HandPos.z);
                ShowMarker = true;
                RingPos = To;
                RingSize = 0.22f;
                ShowRing = true;
                break;
            case STEP.SHOOT:
                if (HeldBall != null && Rim != null)
                {
                    DrawArc(HeldBall.transform.position, Rim.position);
                    ShowArc = true;
                }
                CardTarget = HandPos + Right * 0.3f + Vector3.up * 0.12f;
                break;
            case STEP.DONE:
                CardTarget = CardPos; //その場で弾んで消える
                break;
        }

        //カード：目標の位置へなめらかについていき、ふわふわ揺れながら常にこちらを向く
        float Follow = 1.0f - Mathf.Exp(-6.0f * Time.deltaTime);
        if (CardGroup.alpha <= 0.01f) CardPos = CardTarget; //出てきた瞬間は目標の位置から
        CardPos = Vector3.Lerp(CardPos, CardTarget, Follow);
        Card.position = CardPos + Vector3.up * (0.012f * Mathf.Sin(T * 1.6f) + 0.006f * Mathf.Sin(T * 3.7f + 1.0f));
        Card.rotation = Quaternion.LookRotation(Card.position - CamPos) * Quaternion.Euler(0.0f, 0.0f, 1.5f * Mathf.Sin(T * 0.9f));
        float Age = T - StepTime;
        float Pop = Age < 0.18f ? Mathf.Sin(Age / 0.18f * Mathf.PI * 0.5f) * 1.15f : Mathf.Lerp(1.15f, 1.0f, Mathf.Clamp01((Age - 0.18f) / 0.15f));
        Card.localScale = Vector3.one * CardScale * Pop;
        float FadeOut = (Step == STEP.DONE) ? Mathf.Clamp01((1.6f - Age) / 0.5f) : 1.0f;
        CardGroup.alpha = FadeOut;

        //矢印：From から To へ流れ続ける
        Marker.gameObject.SetActive(ShowMarker);
        if (ShowMarker) FlowChevrons(From, To, CamPos, T);

        //目印のリング：脈打ちながらゆっくり回る
        RingMark.gameObject.SetActive(ShowRing);
        if (ShowRing)
        {
            RingMark.position = RingPos;
            RingMark.rotation = Quaternion.LookRotation(RingPos - CamPos) * Quaternion.Euler(0.0f, 0.0f, T * 40.0f);
            float Beat = 1.0f + 0.12f * Mathf.Sin(T * 5.0f);
            RingMark.localScale = Vector3.one * (RingSize / 100.0f) * Beat;
            RingImage.color = new Color(1.0f, 0.75f, 0.2f, 0.55f + 0.35f * Mathf.Sin(T * 5.0f));
        }

        //弧：点線の模様がリングの方へ流れる
        Arc.gameObject.SetActive(ShowArc);
        if (ShowArc) ArcMat.mainTextureOffset = new Vector2(-T * 3.0f, 0.0f);
    }

    //矢印を From から To へ並べて流す（板はカメラを向き、板の中で矢印の向きを合わせる）
    void FlowChevrons(Vector3 From, Vector3 To, Vector3 CamPos, float T)
    {
        Vector3 Mid = (From + To) * 0.5f;
        Marker.position = Mid;
        Marker.rotation = Quaternion.LookRotation(Mid - CamPos);
        Marker.localScale = Vector3.one * 0.001f; //UIの1単位 = 1mm
        Vector3 Local = Marker.InverseTransformDirection(To - From) / 0.001f; //板の上での向きと長さ（UI単位）
        Vector2 Dir = new Vector2(Local.x, Local.y);
        float Length = Dir.magnitude;
        if (Length < 1.0f) return;
        Dir /= Length;
        float Angle = Mathf.Atan2(Dir.y, Dir.x) * Mathf.Rad2Deg - 90.0f; //矢印の画像は上向き
        for (int i = 0; i < Chevrons.Length; i++)
        {
            float Rate = Mathf.Repeat(T * 0.9f + i / (float)Chevrons.Length, 1.0f); //0で From、1で To
            Chevrons[i].anchoredPosition = Dir * (Length * (Rate - 0.5f));
            Chevrons[i].localEulerAngles = new Vector3(0.0f, 0.0f, Angle);
            float Alpha = Mathf.Sin(Rate * Mathf.PI); //端では消える
            foreach (Image Im in Chevrons[i].GetComponentsInChildren<Image>())
            {
                Im.color = new Color(1.0f, 0.8f, 0.25f, Alpha);
            }
        }
    }

    //ボールからリングへの山なりの弧を引く
    void DrawArc(Vector3 From, Vector3 To)
    {
        const int Points = 28;
        float Height = 0.45f + 0.06f * Vector3.Distance(From, To); //山の高さ（リングへ上から落ちる程度）
        Arc.positionCount = Points;
        for (int i = 0; i < Points; i++)
        {
            float Rate = i / (float)(Points - 1);
            Arc.SetPosition(i, Vector3.Lerp(From, To, Rate) + Vector3.up * (4.0f * Height * Rate * (1.0f - Rate)));
        }
    }

    void SetText(string StepLabel, string Main)
    {
        StepText.text = StepLabel;
        MainText.text = Main;
    }

    void HideAll()
    {
        if (CardGroup != null) CardGroup.alpha = 0.0f;
        if (Marker != null) Marker.gameObject.SetActive(false);
        if (RingMark != null) RingMark.gameObject.SetActive(false);
        if (Arc != null) Arc.gameObject.SetActive(false);
    }

    //見た目を作る **********************************************
    void BuildCard()
    {
        Card = NewCanvas("TutorialCard", new Vector2(380.0f, 170.0f));
        CardGroup = Card.gameObject.AddComponent<CanvasGroup>();
        CardGroup.interactable = false;
        CardGroup.blocksRaycasts = false;

        //本文の板（紺のグラデーション、斜め）
        Image Body = NewImage("Body", Card, new Vector2(10.0f, -12.0f), new Vector2(360.0f, 112.0f), 0.0f, Color.white);
        Body.gameObject.AddComponent<UIGradient>().SetColors(new Color(0.17f, 0.21f, 0.38f), new Color(0.04f, 0.05f, 0.11f));
        Body.gameObject.AddComponent<UISkew>().SetAngle(12.0f);
        Image Edge = NewImage("Edge", Card, new Vector2(-166.0f, -12.0f), new Vector2(12.0f, 112.0f), 0.0f, Orange);
        Edge.gameObject.AddComponent<UISkew>().SetAngle(12.0f);
        MainText = NewText("Main", Body.rectTransform, Vector2.zero, new Vector2(320.0f, 100.0f), 30, Color.white);
        MainText.gameObject.AddComponent<Outline>().effectColor = new Color(0.0f, 0.0f, 0.0f, 0.5f);

        //ステップの札（オレンジ、少し傾けて本文からはみ出させる）
        Image Tag = NewImage("Tag", Card, new Vector2(-92.0f, 58.0f), new Vector2(150.0f, 40.0f), 3.0f, Color.white);
        Tag.gameObject.AddComponent<UIGradient>().SetColors(new Color(1.0f, 0.58f, 0.14f), new Color(0.82f, 0.2f, 0.04f));
        Tag.gameObject.AddComponent<UISkew>().SetAngle(14.0f);
        StepText = NewText("Step", Tag.rectTransform, Vector2.zero, new Vector2(150.0f, 40.0f), 22, Navy);
    }

    void BuildMarkers()
    {
        Marker = NewCanvas("TutorialArrows", new Vector2(10.0f, 10.0f));
        for (int i = 0; i < Chevrons.Length; i++)
        {
            //2本の棒で上向きの山形を作る
            RectTransform C = NewNode("Chevron", Marker, Vector2.zero, new Vector2(60.0f, 40.0f), 0.0f);
            NewImage("L", C, new Vector2(-13.0f, 0.0f), new Vector2(12.0f, 44.0f), -45.0f, Color.white);
            NewImage("R", C, new Vector2(13.0f, 0.0f), new Vector2(12.0f, 44.0f), 45.0f, Color.white);
            Chevrons[i] = C;
        }

        RingMark = NewCanvas("TutorialRing", new Vector2(100.0f, 100.0f));
        RingImage = NewImage("Ring", RingMark, Vector2.zero, new Vector2(100.0f, 100.0f), 0.0f, Color.white);
        RingImage.sprite = RingSprite;
    }

    void BuildArc()
    {
        GameObject Go = new GameObject("TutorialArc");
        Arc = Go.AddComponent<LineRenderer>();
        ArcMat = new Material(GlowMat);
        ArcMat.mainTexture = MakeDotTexture(); //点線になるように丸を繰り返す
        Arc.material = ArcMat;
        Arc.textureMode = LineTextureMode.Tile;
        Arc.textureScale = new Vector2(8.0f, 1.0f); //1mに点が8個
        Arc.widthMultiplier = 0.05f;
        Arc.useWorldSpace = true;
        Arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Gradient G = new Gradient();
        G.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1.0f, 0.8f, 0.3f), 0.0f), new GradientColorKey(Orange, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.15f), new GradientAlphaKey(1.0f, 0.85f), new GradientAlphaKey(0.3f, 1.0f) });
        Arc.colorGradient = G;
    }

    //丸い点の画像（横に繰り返して点線にする）
    Texture2D MakeDotTexture()
    {
        const int Size = 32;
        Texture2D Tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Tex.wrapMode = TextureWrapMode.Repeat;
        Color32[] Px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float D = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(Size * 0.5f, Size * 0.5f)) / (Size * 0.35f);
                byte A = (byte)(255.0f * Mathf.Clamp01(1.0f - D * D));
                Px[y * Size + x] = new Color32(255, 255, 255, A);
            }
        }
        Tex.SetPixels32(Px);
        Tex.Apply();
        return Tex;
    }

    RectTransform NewCanvas(string Name, Vector2 Size)
    {
        GameObject Go = new GameObject(Name, typeof(RectTransform), typeof(Canvas));
        Go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform RT = (RectTransform)Go.transform;
        RT.sizeDelta = Size;
        RT.localScale = Vector3.one * CardScale;
        return RT;
    }

    RectTransform NewNode(string Name, Transform Parent, Vector2 Pos, Vector2 Size, float Rot)
    {
        GameObject Go = new GameObject(Name, typeof(RectTransform));
        RectTransform RT = (RectTransform)Go.transform;
        RT.SetParent(Parent, false);
        RT.anchoredPosition = Pos;
        RT.sizeDelta = Size;
        RT.localEulerAngles = new Vector3(0.0f, 0.0f, Rot);
        return RT;
    }

    Image NewImage(string Name, Transform Parent, Vector2 Pos, Vector2 Size, float Rot, Color C)
    {
        Image Im = NewNode(Name, Parent, Pos, Size, Rot).gameObject.AddComponent<Image>();
        Im.color = C;
        Im.raycastTarget = false;
        return Im;
    }

    Text NewText(string Name, Transform Parent, Vector2 Pos, Vector2 Size, int FontSize, Color C)
    {
        Text Tx = NewNode(Name, Parent, Pos, Size, 0.0f).gameObject.AddComponent<Text>();
        Tx.font = CardFont != null ? CardFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Tx.fontSize = FontSize;
        Tx.color = C;
        Tx.alignment = TextAnchor.MiddleCenter;
        Tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        Tx.verticalOverflow = VerticalWrapMode.Overflow;
        Tx.lineSpacing = 0.9f;
        Tx.raycastTarget = false;
        return Tx;
    }
}
