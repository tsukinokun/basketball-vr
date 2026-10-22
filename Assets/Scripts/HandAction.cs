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
    [SerializeField] float ReleaseDropRatio = 0.95f; //ピークからこの割合まで減速したら指先から離れる
    float PeakSpeed = 0.0f; //構えてからのボールの最高速度
    [SerializeField] Transform Head; //頭（CenterEyeAnchor）。シュートの構えの高さの基準
    [SerializeField] float ShotHeight = -0.25f; //手のひらが目の高さ+この値より上で構えたとみなす（m）
    [SerializeField] float SnapAngularSpeed = 9.0f; //手首のスナップとみなす手の回転の速さ（rad/s）
    [SerializeField] float FingerPadDistance = 0.07f; //手のひらの中心からボールを押す指の腹までの距離（m）
    [SerializeField] float MaxSpin = 19.0f; //ボールの回転の上限（rad/s）。実際のシュートのバックスピンは毎秒2〜3回転ほど
    [SerializeField] float MaxHandSpeed = 12.0f; //これより速い手の動きは追跡の乱れとして無視する（m/s）
    [SerializeField] float MaxHandAngular = 60.0f; //これより速い手の回転は追跡の乱れとして無視する（rad/s）
    [SerializeField] float RegrabDelay = 0.5f; //離してから次に掴めるまでの時間（秒）
    [SerializeField] float MinHoldTime = 0.3f; //掴んでからこの時間は自動で離さない（秒）
    [SerializeField] float MaxReleaseAngle = 75.0f; //これより真上に近い向きの振りは持ち上げとみなして離さない（度）
    [SerializeField] float MinReleaseAngle = 15.0f; //これより下向きの振り（フォロースルーで手が下がる動き）は押し出しとみなさない（度）
    [SerializeField] float TrackingLostGrace = 0.15f; //素手の追跡が切れてもこの時間は掴んだままにする（秒）
    float GrabbedTime; //掴んだ時刻
    float TrackingLostTime = -1.0f; //素手の追跡が切れた時刻（-1 は切れていない）
    Vector3 LostVel, LostSpin; //追跡が切れた瞬間の投げの速度・回転
    float PeakAngular = 0.0f; //構えてからの手の最高回転速度
    Vector3 PeakVel, PeakSpin, PeakArm; //一番速かった瞬間のボールの速度・回転・腕の速度
    Vector3 PeakBallPos; //その瞬間のボールの位置
    float PeakTime; //その時刻
    //素手で投げる：手を開くか、手首のスナップが終わったら離し、直前の手の速さで飛ぶ（VRのよくある作り）
    [SerializeField] float OpenDelta = 20.0f; //持っている間で一番握った時より、指がこれだけ伸びたら手を開いたとみなす（度）
    [SerializeField] float SnapEndRatio = 0.85f; //スナップの回転がピークのこの割合まで落ちたら離す（ピークを過ぎてすぐ）
    [SerializeField] float SnapMaxDelay = 0.08f; //スナップのピークからこの時間たったら、回転が落ちきらなくても離す（秒）
    //シュートアシスト：リングの方へ投げた時だけ、向きと速さをリングに入る値へ近づける
    [SerializeField] float AimAssistAngle = 45.0f; //リングの方向からこの角度以内の投げだけアシストする（度）
    [SerializeField] float AimAssist = 0.0f; //左右の向きをリングへ近づける割合（0 は投げた向きのまま、1 はリングの真ん中）。横に曲がって見えるので使わない
    [SerializeField] float PowerAssist = 0.97f; //強く投げた時に速さをリングに届く速さへ近づける割合（1 は強さに関係なく届く速さ）。軽い投げは必ず届く速さにする
    [SerializeField] float AssistMinSpeed = 1.0f; //これより遅い動き（手を開いて落としただけ）はアシストしない（m/s）
    [SerializeField] float MinSnapAngle = 55.0f; //アシストする時の上向きの角度の下限（度）。弾道が低いとリングの手前に当たってはじかれる
    [SerializeField] float MaxSnapAngle = 68.0f; //アシストする時の上向きの角度の上限（度）
    [SerializeField] Transform RimTarget; //狙うリング（空なら "Rim" という名前の物体）
    [SerializeField] float ForwardAngle = 45.0f; //リングの方向からこの角度以内に動いていれば、リングへ投げる動きとみなす（度）
    [SerializeField] float ForwardMinSpeed = 1.0f; //リングへ投げる動きとみなす最低の速さ（m/s）
    float HoldCurl = 0.0f; //持っている間で一番握った時の指の曲がり具合（度）
    float SnapPeak = 0.0f; //スナップの回転の速さのピーク
    Vector3 SnapPeakSpin; //その時の手の角速度
    float SnapPeakTime; //その時刻
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
    [SerializeField] float VelocityFitWindow = 0.05f; //素手の速度を直線あてはめで求める時間幅（秒）
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
        if (isGrabedByHand)
        {
            //素手で掴んでいる途中に追跡が切れた。コントローラーの値は使わず、前のフレームの値を引き継ぐ
            int Prev = (HistoryHead - 1 + HistorySize) % HistorySize;
            BallVelHistory[HistoryHead] = BallVelHistory[Prev];
            SpinHistory[HistoryHead] = SpinHistory[Prev];
            PalmVelHistory[HistoryHead] = PalmVelHistory[Prev];
            return;
        }
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

    //素手で離す判定（離したら true）。VRのハンドトラッキングでよくある「手を開いたら離す」に加えて、
    //頭上での手首のスナップが終わった時にも離す（ボールは本当は無いので、指を開き忘れても投げられるように）
    bool HandRelease()
    {
        if (Time.time - GrabbedTime < MinHoldTime)
        {
            HoldCurl = HoldPose.Curl; //掴んだ直後は握り具合を測るだけ
            SnapPeak = 0.0f;
            return false;
        }
        //手を開いた：持っている間で一番握った時より指が OpenDelta 度伸びた
        //本物のボールが無いので、持ち運ぶ途中やシュート前に手首を反らせる構えでも指は伸びてしまう
        //開いただけでは落とさず、手がリングの方へ動いている時だけ離す
        HoldCurl = Mathf.Max(HoldCurl, HoldPose.Curl);
        if (HoldCurl - HoldPose.Curl >= OpenDelta && MovingToRim(BallVelHistory[HistoryHead])) return HandThrow("open");

        //頭上でのスナップ：回転がピークを過ぎて落ちたか、ピークから SnapMaxDelay 秒たった
        Vector3 AngVel = SpinHistory[HistoryHead];
        float Angular = AngVel.magnitude;
        if (!IsShotReady())
        {
            SnapPeak = 0.0f; //構えていない間（持ち上げる途中など）の回転は数えない
            return false;
        }
        //スナップに届かない小さな回転は、ReleaseWindow 秒たったら数え直す
        if (Angular > SnapPeak || (SnapPeak < SnapAngularSpeed && Time.time - SnapPeakTime > ReleaseWindow))
        {
            SnapPeak = Angular;
            SnapPeakSpin = AngVel;
            SnapPeakTime = Time.time;
        }
        if (SnapPeak < SnapAngularSpeed) return false;
        if (Angular > SnapPeak * SnapEndRatio && Time.time - SnapPeakTime < SnapMaxDelay) return false;
        //スナップの終わりには手が止まりかけているので、直前 ReleaseWindow 秒にリングの方へ動いたかで見る
        Vector3 Recent, Unused;
        ForwardPeakVelocity(out Recent, out Unused);
        if (!MovingToRim(Recent))
        {
            SnapPeak = 0.0f; //手首を後ろへ反らせた構えの回転。投げではない
            return false;
        }
        return HandThrow("snap");
    }

    //手（指の腹）がリングの方へ投げる速さで動いているか
    bool MovingToRim(Vector3 Velocity)
    {
        if (Velocity.magnitude < ForwardMinSpeed) return false;
        if (Rim() == null) return true; //リングが無ければ向きは問わない
        return Mathf.Abs(AimOffset(Velocity, Item.transform.position)) <= ForwardAngle;
    }

    //離す直前 ReleaseWindow 秒で、リングの方へ一番速く動いていた瞬間の速度と回転
    //手前へ引く構えの動きの方が速いことがあるので、ただ一番速い瞬間ではなく、リングの方向の速さで選ぶ
    void ForwardPeakVelocity(out Vector3 Velocity, out Vector3 Spin)
    {
        Transform Target = Rim();
        if (Target == null)
        {
            PeakVelocity(out Velocity, out Spin);
            return;
        }
        Vector3 ToRim = Target.position - Item.transform.position;
        Vector3 Aim = new Vector3(ToRim.x, 0.0f, ToRim.z).normalized;
        int Best = HistoryHead;
        for (int i = 1; i < HistorySize; i++)
        {
            int Prev = (HistoryHead - i + HistorySize) % HistorySize;
            if (TimeHistory[Prev] <= 0.0f || Time.time - TimeHistory[Prev] > ReleaseWindow) break;
            if (Vector3.Dot(BallVelHistory[Prev], Aim) > Vector3.Dot(BallVelHistory[Best], Aim)) Best = Prev;
        }
        Velocity = BallVelHistory[Best];
        Spin = SpinHistory[Best];
    }

    //素手で投げる。速さは離す直前 ReleaseWindow 秒でリングの方へ一番速かった指の腹の速度（ならしてある）
    bool HandThrow(string Reason)
    {
        Vector3 Velocity, Spin;
        ForwardPeakVelocity(out Velocity, out Spin);
        if (SnapPeak >= SnapAngularSpeed) Spin = SnapPeakSpin; //スナップの回転をバックスピンに
        Vector3 Hand = Velocity;
        Velocity = ShotAssist(Velocity, Item.transform.position);
        Debug.Log("[Throw] " + Reason + " hand=" + Hand.magnitude.ToString("F2") + "m/s handAngle=" + ElevationOf(Hand).ToString("F0") +
        "deg release=" + Velocity.magnitude.ToString("F2") + "m/s angle=" + ElevationOf(Velocity).ToString("F0") +
        "deg aimOff=" + AimOffset(Hand, Item.transform.position).ToString("F0") + "deg curl=" + HoldCurl.ToString("F0") + "->" +
        HoldPose.Curl.ToString("F0") + "deg snapPeak=" + SnapPeak.ToString("F1") + " height=" +
        Item.transform.position.y.ToString("F2") + "m eye=" + EyeHeight().ToString("F2") + "m"); //実機での調整用
        ReleaseBall(Velocity, Spin);
        return true;
    }

    //リングを探す（無ければ null）
    Transform Rim()
    {
        if (RimTarget == null)
        {
            GameObject Found = GameObject.Find("Rim");
            if (Found != null) RimTarget = Found.transform;
        }
        return RimTarget;
    }

    //投げた左右の向きとリングの方向の角度（度。リングが無い・真上に投げた時は 180）
    float AimOffset(Vector3 Velocity, Vector3 From)
    {
        Transform Target = Rim();
        Vector3 Flat = new Vector3(Velocity.x, 0.0f, Velocity.z);
        if (Target == null || Flat.sqrMagnitude < 1e-4f) return 180.0f;
        Vector3 ToRim = Target.position - From;
        return Vector3.SignedAngle(Flat, new Vector3(ToRim.x, 0.0f, ToRim.z), Vector3.up);
    }

    //シュートアシスト（VRのバスケットボールでよくある補助）
    //リングの方へ投げた時だけ、速さを PowerAssist の割合でリングまで届く値へ近づける（左右は AimAssist、既定では補正しない）
    //上下の角度は手の振りのまま（低すぎ・高すぎは範囲に収める）。リングと違う方へ投げた時はそのまま
    Vector3 ShotAssist(Vector3 Velocity, Vector3 From)
    {
        float Offset = AimOffset(Velocity, From);
        if (Velocity.magnitude < AssistMinSpeed || Mathf.Abs(Offset) > AimAssistAngle) return Velocity;
        Vector3 Flat = new Vector3(Velocity.x, 0.0f, Velocity.z).normalized;
        Flat = Quaternion.AngleAxis(Offset * AimAssist, Vector3.up) * Flat;
        float Elevation = Mathf.Clamp(ElevationOf(Velocity), MinSnapAngle, MaxSnapAngle) * Mathf.Deg2Rad;
        Vector3 Dir = Flat * Mathf.Cos(Elevation) + Vector3.up * Mathf.Sin(Elevation);
        //軽く投げても届く速さに。届く速さより強く投げた時だけ、その分の (1 - PowerAssist) だけ遠くへ飛ぶ
        float Reach = ReachSpeed(From, Flat, Elevation);
        float Speed = Velocity.magnitude;
        if (Reach > 0.0f) Speed = (Speed > Reach) ? Mathf.Lerp(Speed, Reach, PowerAssist) : Reach;
        return Dir * Speed;
    }

    //From から Aim の向き・上向き Elevation（rad）で投げてリングの高さに落ちてくる時、
    //リングの中心までの距離に届く速さ（届かない角度・リングが見つからない時は 0）
    float ReachSpeed(Vector3 From, Vector3 Aim, float Elevation)
    {
        Transform Target = Rim();
        if (Target == null) return 0.0f;
        Vector3 ToRim = Target.position - From;
        float Distance = Vector3.Dot(new Vector3(ToRim.x, 0.0f, ToRim.z), Aim); //投げる向きに沿ったリングまでの距離
        float Rise = ToRim.y;
        float Cos = Mathf.Cos(Elevation);
        float Lift = Distance * Mathf.Tan(Elevation) - Rise; //この角度の直線がリングの上を通る高さ
        if (Distance <= 0.0f || Lift <= 0.0f) return 0.0f;
        float G = -Physics.gravity.y;
        float Vacuum = Mathf.Sqrt(G * Distance * Distance / (2.0f * Cos * Cos * Lift)); //空気抵抗が無い時に届く速さ
        //空気抵抗があると届かないので、ボールと同じ空気抵抗で軌道を計算して、ちょうど届く速さを二分探索で求める
        BallAction Ball = Item.GetComponent<BallAction>();
        float Drag = (Ball != null) ? Ball.DragPerMass : 0.0f;
        if (Drag <= 0.0f) return Vacuum;
        float Low = Vacuum, High = Vacuum * 1.3f;
        for (int i = 0; i < 16; i++)
        {
            float Mid = 0.5f * (Low + High);
            if (FlightDistance(Mid, Elevation, Rise, Drag) < Distance) Low = Mid;
            else High = Mid;
        }
        return 0.5f * (Low + High);
    }

    //速さ Speed・上向き Elevation（rad）で投げ、空気抵抗（Drag = 係数÷質量）を受けて
    //Rise だけ上の高さに落ちてくるまでの水平距離（物理エンジンと同じく FixedUpdate の刻みで計算）
    static float FlightDistance(float Speed, float Elevation, float Rise, float Drag)
    {
        float Dt = Time.fixedDeltaTime;
        float G = -Physics.gravity.y;
        float X = 0.0f, Y = 0.0f;
        float Vx = Speed * Mathf.Cos(Elevation), Vy = Speed * Mathf.Sin(Elevation);
        for (int Step = 0; Step < 1000; Step++)
        {
            float S = Mathf.Sqrt(Vx * Vx + Vy * Vy);
            Vx -= Drag * S * Vx * Dt;
            Vy -= (G + Drag * S * Vy) * Dt;
            float PrevX = X, PrevY = Y;
            X += Vx * Dt;
            Y += Vy * Dt;
            if (Vy < 0.0f && Y <= Rise)
            {
                float T = (PrevY - Rise) / (PrevY - Y); //この刻みの中でリングの高さを横切った位置
                return Mathf.Lerp(PrevX, X, T);
            }
        }
        return X;
    }

    //目の高さ（調整用ログ）
    float EyeHeight()
    {
        Transform Eye = (Head != null) ? Head : (Camera.main != null ? Camera.main.transform : null);
        return (Eye != null) ? Eye.position.y : 0.0f;
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
        TrackingLostTime = -1.0f;
        SnapPeak = 0.0f;
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
            //手首のスナップの回転をバックスピンにする。素手の骨の回転は向きがぶれることがあるので、
            //向きは投げる方向から決め、速さだけを手首の回転（投げる方向に垂直な横軸まわりの分）から取る
            Vector3 SpinAxis = Vector3.Cross(Velocity, Vector3.up).normalized; //この軸の正の向きがバックスピン
            float Snap = Mathf.Abs(Vector3.Dot(Spin, SpinAxis));
            Item.gameObject.SendMessage("Spin", SpinAxis * Mathf.Min(Snap * SpinRate, MaxSpin),
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
        else if (VRAction.isVR && !isGrabedByHand) //素手で掴んだまま追跡が切れた時は、最後の手のひらの位置に残す
        {
            transform.localPosition = ControllerSensorPos;
            mySensor.radius = ControllerSensorRadius;
        }
        RecordPosition();
        if (!Item)
        {
            isGrabed = false;
            isGrabedByHand = false;
            TrackingLostTime = -1.0f;
            return; //ボールに侵入していないなら以降の処理は不要
        }
        //素手で掴んでいる途中にトラッキングが切れた。一瞬の途切れ（指で隠れる等）では離さず、
        //TrackingLostGrace 秒切れたままなら、切れた瞬間までの振りで投げる（速い振りで切れやすい）
        if (isGrabedByHand && !Hand)
        {
            //スナップの途中で切れた（速いスナップで切れやすい）なら、そのスナップで投げる
            if (SnapPeak >= SnapAngularSpeed && IsShotReady() && HandThrow("trackingLost")) return;
            if (TrackingLostTime < 0.0f)
            {
                TrackingLostTime = Time.time;
                PeakVelocity(out LostVel, out LostSpin);
            }
            if (Time.time - TrackingLostTime > TrackingLostGrace)
            {
                Debug.Log("[Release] trackingLost release=" + LostVel.magnitude.ToString("F2") + "m/s angle=" +
                ElevationOf(LostVel).ToString("F0") + "deg spin=" + LostSpin.magnitude.ToString("F1")); //実機での調整用
                ReleaseBall(ShotAssist(LostVel, Item.transform.position), LostSpin);
            }
            return;
        }
        if (TrackingLostTime >= 0.0f)
        {
            //猶予内に追跡が戻った。途切れる前の振りは数え直す
            TrackingLostTime = -1.0f;
            PeakSpeed = 0.0f;
            PeakAngular = 0.0f;
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
            SnapPeak = 0.0f;
            GrabbedTime = Time.time;
            isGrabed = true;
        }
        //素手は手を開くか、頭上でのスナップで投げる
        if (isGrabed && isGrabedByHand)
        {
            if (HandRelease()) return;
        }
        //コントローラーは、頭の上に構えてから手首をスナップし、手が減速した瞬間にボールが指先から離れる
        //持ち上げる途中の振りでは離さない
        else if (isGrabed && VRAction.isVR && AutoRelease)
        {
            float Speed = BallVelHistory[HistoryHead].magnitude;
            if (IsShotReady())
            {
                //前上方へ押し出している間だけピークを取る（頭上から振り下ろすフォロースルーの方が速いので、数えると下向きに投げてしまう）
                float Elevation = ElevationOf(BallVelHistory[HistoryHead]);
                if (Speed > PeakSpeed && Elevation >= MinReleaseAngle && Elevation <= MaxReleaseAngle)
                {
                    //一番速かった瞬間の値を覚えておく（離すのはこの値で）
                    PeakSpeed = Speed;
                    PeakVel = BallVelHistory[HistoryHead];
                    PeakSpin = SpinHistory[HistoryHead];
                    PeakArm = PalmVelHistory[HistoryHead];
                    PeakBallPos = Item.transform.position;
                    PeakTime = Time.time;
                }
                PeakAngular = Mathf.Max(PeakAngular, SpinHistory[HistoryHead].magnitude);
                //ピークから ReleaseWindow 秒たっても離れていないなら、途中でやめた振り。数え直す
                if (PeakSpeed > 0.0f && Time.time - PeakTime > ReleaseWindow)
                {
                    PeakSpeed = 0.0f;
                    PeakAngular = 0.0f;
                }
            }
            else
            {
                PeakSpeed = 0.0f; //構えていない間（持ち上げる途中など）の速さは数えない
                PeakAngular = 0.0f;
            }
            //押し出しのピークを過ぎて減速したか、手が下向きに返り始めたら指先から離れる
            //掴んだ直後と、真上に近い振り（持ち上げ）では離さない
            bool PastPeak = Speed < PeakSpeed * ReleaseDropRatio ||
                ElevationOf(BallVelHistory[HistoryHead]) < MinReleaseAngle;
            if (PeakSpeed > AutoReleaseSpeed && PeakAngular > SnapAngularSpeed && PastPeak &&
            Time.time - GrabbedTime >= MinHoldTime && ElevationOf(PeakVel) <= MaxReleaseAngle)
            {
                Vector3 Velocity = PeakVel, Spin = PeakSpin;
                Vector3 Arm = PeakArm; //手のひら（腕の動き）の速度
                float Delay = Time.time - PeakTime; //ピークから離れるまでの時間
                //離すのを判定するまでの遅れの分、ピークで離れていたら今いるはずの位置からボールを飛ばす
                Item.transform.position = PeakBallPos + Velocity * Delay + 0.5f * Physics.gravity * Delay * Delay;
                Debug.Log("[Shot] peakSpeed=" + PeakSpeed.ToString("F2") + " peakAngular=" + PeakAngular.ToString("F1") +
                " height=" + PeakBallPos.y.ToString("F2") + "m eye=" + EyeHeight().ToString("F2") + "m" +
                " release=" + Velocity.magnitude.ToString("F2") + "m/s angle=" + ElevationOf(Velocity).ToString("F0") + "deg" +
                " arm=" + Arm.magnitude.ToString("F2") + "m/s armAngle=" + ElevationOf(Arm).ToString("F0") + "deg" +
                " spin=" + Spin.magnitude.ToString("F1") +
                " backspin=" + Vector3.Dot(Spin, Vector3.Cross(Velocity, Vector3.up).normalized).ToString("F1") + " delay=" + (Delay * 1000.0f).ToString("F0") + "ms"); //実機での調整用
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
