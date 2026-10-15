using UnityEngine;

public class HandAction : MonoBehaviour
{
    [SerializeField] GameObject VisibleBall; //見える手の球
    [SerializeField] GameObject Manager; //マネージャー
    [SerializeField] float ThrownJudge = 0.5f; //投げたと判定する手の速さ（m/s）
    [SerializeField] float VelocityWindow = 0.08f; //手の速度を測る時間幅（秒）
    [SerializeField] float ThrowBoostVR = 1.0f; //投げる力の倍率（VR）
    [SerializeField] float ThrowBoostPC = 1.6f; //投げる力の倍率（PC）
    [SerializeField] Vector3 HoldOffsetVR = new Vector3(-0.09f, -0.01f, 0.05f); //VRで掴んだボールの位置（手のひら側）
    [SerializeField] float ReleaseWindow = 0.2f; //離す直前のこの時間内で一番速い瞬間を投げの速度にする（秒）
    [SerializeField] float SpinRate = 1.0f; //手首の回転をボールの回転に移す割合
    [SerializeField] BallHoldPose HoldPose; //ボールを持つ手の形（VR）
    [SerializeField] bool AutoRelease = true; //スナップで手が減速したら自動でボールを離す（VR）
    [SerializeField] float AutoReleaseSpeed = 3.0f; //自動で離す振りとみなすボールの速さ（m/s）
    [SerializeField] float ReleaseDropRatio = 0.85f; //ピークからこの割合まで減速したら指先から離れる
    float PeakSpeed = 0.0f; //構えてからのボールの最高速度
    [SerializeField] Transform Head; //頭（CenterEyeAnchor）。シュートの構えの高さの基準
    [SerializeField] float ShotHeight = -0.1f; //手のひらが目の高さ+この値より上で構えたとみなす（m）
    [SerializeField] float SnapAngularSpeed = 20.0f; //手首のスナップとみなす手の回転の速さ（rad/s）
    [SerializeField] float FingerPadDistance = 0.07f; //手のひらの中心からボールを押す指の腹までの距離（m）
    [SerializeField] float MaxSpin = 20.0f; //ボールの回転の上限（rad/s）。実際のシュートのバックスピンは毎秒2〜3回転ほど
    [SerializeField] float MaxHandSpeed = 12.0f; //これより速い手の動きは追跡の乱れとして無視する（m/s）
    [SerializeField] float MaxHandAngular = 60.0f; //これより速い手の回転は追跡の乱れとして無視する（rad/s）
    [SerializeField] float RegrabDelay = 0.5f; //離してから次に掴めるまでの時間（秒）
    float PeakAngular = 0.0f; //構えてからの手の最高回転速度
    Vector3 PeakVel, PeakSpin, PeakArm; //一番速かった瞬間のボールの速度・回転・腕の速度
    float PeakTime; //その時刻
    float ReleasedTime = -10.0f; //最後に離した時刻
    const int HistorySize = 32;
    Vector3[] PosHistory = new Vector3[HistorySize]; //手のワールド座標の履歴
    float[] TimeHistory = new float[HistorySize]; //その時刻
    Vector3[] BallVelHistory = new Vector3[HistorySize]; //ボール位置での速度の履歴（VR）
    Vector3[] SpinHistory = new Vector3[HistorySize]; //手の角速度の履歴（VR）
    int HistoryHead = 0;
    GameObject Item = null; //侵入中のボール
    bool isGrabed = false; //物体を掴んでいるかの真偽値

    //素手（ハンドトラッキング）で遊ぶ時の設定
    [SerializeField] float HandSensorRadius = 0.04f; //手のひらの当たり判定の半径
    bool isGrabedByHand = false; //素手で掴んでいるか
    SphereCollider mySensor; //自身の当たり判定
    float ControllerSensorRadius; //コントローラー時の当たり判定の半径
    Vector3 ControllerSensorPos; //コントローラー時の当たり判定の位置
    [SerializeField] float VelocityFitWindow = 0.07f; //素手の速度を直線あてはめで求める時間幅（秒）
    Vector3[] ContactHistory = new Vector3[HistorySize]; //ボールを押す指の腹の位置の履歴（素手）
    Vector3[] PalmPosHistory = new Vector3[HistorySize]; //手のひらの位置の履歴（素手）
    Quaternion[] PalmRotHistory = new Quaternion[HistorySize]; //手のひらの回転の履歴（素手）
    bool[] HandSampleValid = new bool[HistorySize]; //素手の記録があるか
    Vector3[] PalmVelHistory = new Vector3[HistorySize]; //手のひら（腕）の速度の履歴（調整用ログ）

    //シュートの構え（ボールを持った手が頭の高さまで上がっている）か
    bool IsShotReady()
    {
        Transform Eye = (Head != null) ? Head : (Camera.main != null ? Camera.main.transform : null);
        if (Eye == null) return true;
        return transform.position.y >= Eye.position.y + ShotHeight;
    }

    //素手がトラッキングされているか
    bool IsHandTracked()
    {
        return VRAction.isVR && HoldPose != null && HoldPose.PalmBone != null;
    }

    void Start()
    {
        mySensor = GetComponent<SphereCollider>();
        ControllerSensorRadius = mySensor.radius;
        ControllerSensorPos = transform.localPosition;
        if (VRAction.isVR)
        {
            //VR時なら、見える手の球を見えなくする
            VisibleBall.GetComponent<MeshRenderer>().enabled = false;
        }
        else
        {
            //PC時なら、コライダーを見える手の球と同じ位置に
            transform.localPosition = VisibleBall.transform.localPosition;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        //空の手がボールに侵入した瞬間
        if (other.gameObject.tag == "Item" && !isGrabed)
        {
            other.gameObject.SendMessage("ChangeColor", Color.green,
            SendMessageOptions.DontRequireReceiver); //緑に
            Item = other.gameObject; //侵入ボールを記録
        }
    }
    void OnTriggerExit(Collider other)
    {
        //空の手がボールから出た瞬間
        if (other.gameObject.tag == "Item" && !isGrabed)
        {
            other.gameObject.SendMessage("ChangeColor", Color.white,
            SendMessageOptions.DontRequireReceiver); //通常色に
            Item = null; //侵入ボールは無し
        }
    }

    //手の位置を毎フレーム記録する
    void RecordPosition()
    {
        HistoryHead = (HistoryHead + 1) % HistorySize;
        PosHistory[HistoryHead] = transform.position;
        TimeHistory[HistoryHead] = Time.time;
        if (VRAction.isVR)
        {
            RecordControllerVelocity();
        }
    }

    //コントローラーのセンサー速度から、ボールを押し出す点の速度を求めて記録する（VR）
    //ボールは手に固定されていないので、ボールの中心ではなく手のひら（握り＝アンカー）の速度で飛ぶ
    void RecordControllerVelocity()
    {
        if (IsHandTracked())
        {
            RecordHandVelocity();
            return;
        }
        HandSampleValid[HistoryHead] = false;
        Transform Anchor = transform.parent; //RightHandAnchor
        Transform Space = Anchor.parent; //TrackingSpace
        Vector3 LinVel = Space.TransformVector(
            OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch));
        Vector3 AngVel = Space.TransformDirection(
            OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.RTouch));
        BallVelHistory[HistoryHead] = LinVel; //アンカーの速度に手首の回転の分は含まれている
        SpinHistory[HistoryHead] = AngVel;
    }

    //素手の時は、手のひらの骨の動きから速度と角速度を求める（素手にはコントローラーの速度センサーが無い）
    //ボールは手に固定されておらず、指の腹に押し出されて飛ぶ。なのでボールの速度は「指の腹の速度」
    //= 指の腹の位置の変化。手首の回転の分も位置の変化に含まれる。スナップの勢いは手のひらの面に垂直（上・前）にかかる
    //手の追跡は描画より更新が遅く、1フレームごとの差だと「止まる→飛ぶ」を繰り返すので、
    //直近 VelocityFitWindow 秒の位置に直線をあてはめた傾きを速度にする
    void RecordHandVelocity()
    {
        Transform Palm = HoldPose.PalmBone;
        int Head = HistoryHead;
        ContactHistory[Head] = Palm.position + HoldPose.FingerDirection * FingerPadDistance; //ボールを押す指の腹
        PalmPosHistory[Head] = Palm.position;
        PalmRotHistory[Head] = Palm.rotation;
        HandSampleValid[Head] = true;

        Vector3 ContactVel = FitVelocity(ContactHistory);
        Vector3 PalmVel = FitVelocity(PalmPosHistory);
        //手の回転の速さ：あてはめ区間の最初と最後の回転の差
        Vector3 AngVel = Vector3.zero;
        int Oldest = OldestHandSample();
        float Dt = TimeHistory[Head] - TimeHistory[Oldest];
        if (Dt > 0.0f)
        {
            float Angle;
            Vector3 Axis;
            (PalmRotHistory[Head] * Quaternion.Inverse(PalmRotHistory[Oldest])).ToAngleAxis(out Angle, out Axis);
            if (Angle > 180.0f) Angle -= 360.0f;
            if (!float.IsInfinity(Axis.x)) AngVel = Axis * (Angle * Mathf.Deg2Rad / Dt);
        }
        //人の手では出ない速さは追跡が一瞬飛んだもの。前のフレームの値で置き換える
        if (PalmVel.magnitude > MaxHandSpeed || AngVel.magnitude > MaxHandAngular)
        {
            int Prev = (Head - 1 + HistorySize) % HistorySize;
            BallVelHistory[Head] = BallVelHistory[Prev];
            SpinHistory[Head] = SpinHistory[Prev];
            PalmVelHistory[Head] = PalmVelHistory[Prev];
            return;
        }
        BallVelHistory[Head] = ContactVel;
        SpinHistory[Head] = AngVel;
        PalmVelHistory[Head] = PalmVel;
    }

    //直近 VelocityFitWindow 秒に入る、一番古い素手の記録の番号
    int OldestHandSample()
    {
        int Oldest = HistoryHead;
        for (int i = 1; i < HistorySize; i++)
        {
            int Prev = (HistoryHead - i + HistorySize) % HistorySize;
            if (!HandSampleValid[Prev] || TimeHistory[HistoryHead] - TimeHistory[Prev] > VelocityFitWindow) break;
            Oldest = Prev;
        }
        return Oldest;
    }

    //直近 VelocityFitWindow 秒の位置に直線（最小二乗法）をあてはめて速度を求める
    Vector3 FitVelocity(Vector3[] Positions)
    {
        int Oldest = OldestHandSample();
        int Count = (HistoryHead - Oldest + HistorySize) % HistorySize + 1;
        if (Count < 2) return Vector3.zero;
        float MeanT = 0.0f;
        Vector3 MeanP = Vector3.zero;
        for (int i = 0; i < Count; i++)
        {
            int Index = (Oldest + i) % HistorySize;
            MeanT += TimeHistory[Index];
            MeanP += Positions[Index];
        }
        MeanT /= Count;
        MeanP /= Count;
        float Stt = 0.0f;
        Vector3 Stp = Vector3.zero;
        for (int i = 0; i < Count; i++)
        {
            int Index = (Oldest + i) % HistorySize;
            float Dt = TimeHistory[Index] - MeanT;
            Stt += Dt * Dt;
            Stp += Dt * (Positions[Index] - MeanP);
        }
        return (Stt > 0.0f) ? Stp / Stt : Vector3.zero;
    }

    //離す直前 ReleaseWindow 秒で一番速かった瞬間の速度と回転を求める（VR）
    //トリガーを離すのは振りのピークより少し遅れるので、離した瞬間の値だと失速した球になる
    void PeakVelocity(out Vector3 Velocity, out Vector3 Spin)
    {
        int Best = HistoryHead;
        for (int i = 1; i < HistorySize; i++)
        {
            int Prev = (HistoryHead - i + HistorySize) % HistorySize;
            if (TimeHistory[Prev] <= 0.0f || Time.time - TimeHistory[Prev] > ReleaseWindow) break;
            if (BallVelHistory[Prev].sqrMagnitude > BallVelHistory[Best].sqrMagnitude) Best = Prev;
        }
        //速度は記録の時点でならしてあるので、ピークの値をそのまま使う
        Velocity = BallVelHistory[Best];
        Spin = SpinHistory[Best];
    }

    //速度の上向きの角度（度）
    static float ElevationOf(Vector3 V)
    {
        return (V.sqrMagnitude > 0.0f) ? Mathf.Asin(Mathf.Clamp(V.normalized.y, -1.0f, 1.0f)) * Mathf.Rad2Deg : 0.0f;
    }

    //直近 VelocityWindow 秒の移動から手の速度を求める
    Vector3 HandVelocity()
    {
        int Index = HistoryHead;
        for (int i = 1; i < HistorySize; i++)
        {
            int Prev = (HistoryHead - i + HistorySize) % HistorySize;
            if (TimeHistory[Prev] <= 0.0f) break; //まだ記録がない
            Index = Prev;
            if (Time.time - TimeHistory[Prev] >= VelocityWindow) break;
        }
        float Dt = TimeHistory[HistoryHead] - TimeHistory[Index];
        if (Dt <= 0.0f) return Vector3.zero;
        return (PosHistory[HistoryHead] - PosHistory[Index]) / Dt;
    }

    //ボールを手から離す（Velocity : ボールの速度、Spin : ボールの回転）
    void ReleaseBall(Vector3 Velocity, Vector3 Spin)
    {
        if (HoldPose != null)
        {
            HoldPose.Release(); //手の形を元に戻す
        }
        Item.transform.parent = null; //ボールの親を無しにする
        Item.gameObject.SendMessage("Grabed", false,
        SendMessageOptions.DontRequireReceiver); //掴んでいない指示(false)
        isGrabed = false;
        isGrabedByHand = false;
        ReleasedTime = Time.time;

        //十分な速さなら投げたとみなす
        if (Velocity.magnitude > ThrownJudge)
        {
            //マネージャーに鳴音依頼
            Manager.SendMessage("SoundThrown",
            SendMessageOptions.DontRequireReceiver);
            //ボールに速度を渡す
            Item.gameObject.SendMessage("Thrown",
            Velocity * (VRAction.isVR ? ThrowBoostVR : ThrowBoostPC),
            SendMessageOptions.DontRequireReceiver);
            //手首の回転をボールに移す（スナップでバックスピンがかかる）
            Item.gameObject.SendMessage("Spin", Vector3.ClampMagnitude(Spin * SpinRate, MaxSpin),
            SendMessageOptions.DontRequireReceiver);
        }
        Item = null; //投げたボールはもう掴まない
        if (VRAction.isVR)
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch); //振動停止
        }
    }

    void Update()
    {
        bool Hand = IsHandTracked();
        if (Hand)
        {
            //素手の時は当たり判定を手のひらに置く
            transform.position = HoldPose.PalmBone.position;
            mySensor.radius = HandSensorRadius;
        }
        else if (VRAction.isVR)
        {
            transform.localPosition = ControllerSensorPos;
            mySensor.radius = ControllerSensorRadius;
        }
        RecordPosition();
        if (!Item)
        {
            isGrabed = false;
            isGrabedByHand = false;
            return; //ボールに侵入していないなら以降の処理は不要
        }
        //素手で掴んでいる途中にトラッキングが切れたら、そこまでの振りで投げる（速い振りで切れやすい）
        if (isGrabedByHand && !Hand)
        {
            Vector3 Velocity, Spin;
            PeakVelocity(out Velocity, out Spin);
            ReleaseBall(Velocity, Spin);
            return;
        }
        //掴む：素手は手のひらが触れたら、コントローラーは右手トリガー、PCはSpace
        bool GrabNow = Hand ? !isGrabed
            : (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger) || Input.GetKeyDown(KeyCode.Space));
        //離した直後は、まだ手に触れているボールをすぐ掴み直さない
        if (GrabNow && Time.time - ReleasedTime >= RegrabDelay)
        {
            isGrabedByHand = Hand;
            Item.transform.parent = transform; //ボールの親を右手にする
            Item.gameObject.SendMessage("Grabed", true,
            SendMessageOptions.DontRequireReceiver); //掴んでいる指示(true)
            if (VRAction.isVR)
            {
                Item.transform.localPosition = HoldOffsetVR; //手のひらに収める（骨データが無い時の位置）
                if (HoldPose != null)
                {
                    SphereCollider Col = Item.GetComponent<SphereCollider>();
                    float Radius = Col.radius * Item.transform.lossyScale.x;
                    HoldPose.Hold(Item.transform, Radius); //手のひらに乗せて指を沿わせる
                }
            }
            PeakSpeed = 0.0f;
            PeakAngular = 0.0f;
            isGrabed = true;
        }
        //頭の上に構えてから手首をスナップし、手が減速した瞬間にボールが指先から離れる（VR）
        //持ち上げる途中の振りでは離さない
        if (isGrabed && VRAction.isVR && (AutoRelease || isGrabedByHand))
        {
            float Speed = BallVelHistory[HistoryHead].magnitude;
            if (IsShotReady())
            {
                if (Speed > PeakSpeed)
                {
                    //一番速かった瞬間の値を覚えておく（離すのはこの値で）
                    PeakSpeed = Speed;
                    PeakVel = BallVelHistory[HistoryHead];
                    PeakSpin = SpinHistory[HistoryHead];
                    PeakArm = PalmVelHistory[HistoryHead];
                    PeakTime = Time.time;
                }
                PeakAngular = Mathf.Max(PeakAngular, SpinHistory[HistoryHead].magnitude);
            }
            else
            {
                PeakSpeed = 0.0f; //構えていない間（持ち上げる途中など）の速さは数えない
                PeakAngular = 0.0f;
            }
            if (PeakSpeed > AutoReleaseSpeed && PeakAngular > SnapAngularSpeed &&
            Speed < PeakSpeed * ReleaseDropRatio)
            {
                Vector3 Velocity = PeakVel, Spin = PeakSpin;
                Vector3 Arm = PeakArm; //手のひら（腕の動き）の速度
                float Delay = Time.time - PeakTime; //ピークから離れるまでの時間
                Debug.Log("[Shot] peakSpeed=" + PeakSpeed.ToString("F2") + " peakAngular=" + PeakAngular.ToString("F1") +
                " release=" + Velocity.magnitude.ToString("F2") + "m/s angle=" + ElevationOf(Velocity).ToString("F0") + "deg" +
                " arm=" + Arm.magnitude.ToString("F2") + "m/s armAngle=" + ElevationOf(Arm).ToString("F0") + "deg" +
                " spin=" + Spin.magnitude.ToString("F1") + " delay=" + (Delay * 1000.0f).ToString("F0") + "ms"); //実機での調整用
                ReleaseBall(Velocity, Spin);
                return;
            }
        }
        //離す：コントローラーは右手トリガー、PCはSpaceを離したら（素手はスナップでのみ離れる）
        bool ReleaseNow = !isGrabedByHand &&
            (OVRInput.GetUp(OVRInput.RawButton.RIndexTrigger) || Input.GetKeyUp(KeyCode.Space));
        if (isGrabed && ReleaseNow)
        {
            //VRはセンサー速度のピーク（手首のスナップ込み）、PCは位置履歴から求めた手の速度
            Vector3 Velocity;
            Vector3 Spin = Vector3.zero;
            if (VRAction.isVR)
            {
                PeakVelocity(out Velocity, out Spin);
            }
            else
            {
                Velocity = HandVelocity();
            }
            ReleaseBall(Velocity, Spin);
            return;
        }
        //VRコントローラーのバイブレーション
        if (VRAction.isVR)
        {
            OVRInput.SetControllerVibration(0f, isGrabed ? 1f : 0f, OVRInput.Controller.RTouch);
        }
    }
}
