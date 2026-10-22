using System.Collections.Generic;
using UnityEngine;

//ボールを持っている間、手のひらにボールを乗せ、指をボールの表面に沿わせる
//OVRSkeleton が Update で骨を更新した後、LateUpdate で上書きする
[RequireComponent(typeof(OVRSkeleton))]
public class BallHoldPose : MonoBehaviour
{
    [SerializeField] bool isRightHand = true; //右手か
    [SerializeField] float PalmGap = 0.015f; //手のひらとボール表面の隙間
    [SerializeField] float FingerThickness = 0.01f; //指の太さ（関節の中心からボール表面まで）
    [SerializeField] float CurlStep = 2.0f; //指を曲げる刻み（度）
    [SerializeField] float MaxCurl = 90.0f; //1関節で曲げる最大角度（度）

    //指ごとの関節（根元から順）。最後は指先
    static readonly string[][] Fingers =
    {
        new[] { "ThumbProximal", "ThumbDistal", "ThumbTip" },
        new[] { "IndexProximal", "IndexIntermediate", "IndexDistal", "IndexTip" },
        new[] { "MiddleProximal", "MiddleIntermediate", "MiddleDistal", "MiddleTip" },
        new[] { "RingProximal", "RingIntermediate", "RingDistal", "RingTip" },
        new[] { "LittleProximal", "LittleIntermediate", "LittleDistal", "LittleTip" },
    };

    OVRSkeleton mySkeleton;
    Dictionary<string, int> BoneIndex; //骨の名前 → Bones の番号
    int BoneCount; //BoneIndex を作った時の骨の数
    Transform Ball; //持っているボール
    float BallRadius;
    float PalmSide = 0.0f; //手のひらの向きの符号（0 は未決定）

    void Awake()
    {
        mySkeleton = GetComponent<OVRSkeleton>();
    }

    //ボールを持つ ******************
    public void Hold(Transform _ball, float _radius)
    {
        Ball = _ball;
        BallRadius = _radius;
        PalmSide = 0.0f;
    }

    //ボールを離す ******************
    public void Release()
    {
        Ball = null;
    }

    //骨データが使えて、手の形を作れるか
    public bool IsReady
    {
        get { return mySkeleton != null && mySkeleton.IsInitialized && mySkeleton.IsDataValid; }
    }

    //手のひらの骨（位置・回転はトラッキングのまま、上書きしない）
    public Transform PalmBone { get { return IsReady ? Bone("Palm") : null; } }

    //指の付け根へ向かう方向（手首 → 中指の付け根）。付け根より根元の骨は上書きしない
    public Vector3 FingerDirection
    {
        get
        {
            Transform Wrist = Bone("Wrist"), MiddleProx = Bone("MiddleProximal");
            if (!IsReady || Wrist == null || MiddleProx == null) return Vector3.zero;
            return (MiddleProx.position - Wrist.position).normalized;
        }
    }

    //指の曲がり具合（人差し指〜小指の、付け根から指先への向きと手の向きの角度の平均。0 で伸びきり、度）
    //持っている間は指をボールに沿わせて上書きするので、上書きする前のトラッキングの値を LateUpdate で覚えておく
    //手を開いたかの判定に使う
    float TrackedCurl = 0.0f;
    public float Curl { get { return IsReady ? TrackedCurl : 0.0f; } }

    //骨の名前（XRHand_ を除いた部分）から Bones の番号を引く（無ければ -1）
    //BoneId は手と体で同じ値が重複していて ToString() が別名になるので、名前ではなく値で照合する
    int IndexOf(string Name)
    {
        if (BoneIndex == null || BoneCount != mySkeleton.Bones.Count)
        {
            BoneIndex = new Dictionary<string, int>();
            BoneCount = mySkeleton.Bones.Count;
        }
        int Index;
        if (BoneIndex.TryGetValue(Name, out Index)) return Index;
        Index = -1;
        OVRSkeleton.BoneId Id;
        if (System.Enum.TryParse("XRHand_" + Name, out Id))
        {
            for (int i = 0; i < mySkeleton.Bones.Count; i++)
            {
                if ((int)mySkeleton.Bones[i].Id == (int)Id)
                {
                    Index = i;
                    break;
                }
            }
        }
        BoneIndex[Name] = Index;
        return Index;
    }

    //骨の名前から Transform を引く
    Transform Bone(string Name)
    {
        int Index = IndexOf(Name);
        return (Index >= 0) ? mySkeleton.Bones[Index].Transform : null;
    }

    //開いた手（バインドポーズ）の向きに戻す
    void ResetToBindPose(string Name)
    {
        int Index = IndexOf(Name);
        if (Index < 0 || Index >= mySkeleton.BindPoses.Count) return;
        mySkeleton.Bones[Index].Transform.localRotation = mySkeleton.BindPoses[Index].Transform.localRotation;
    }

    void LateUpdate()
    {
        if (!IsReady) return;
        Vector3 HandDir = FingerDirection;
        if (HandDir != Vector3.zero)
        {
            float Sum = 0.0f;
            int Count = 0;
            for (int f = 1; f < Fingers.Length; f++) //親指は除く
            {
                Transform Root = Bone(Fingers[f][0]), Tip = Bone(Fingers[f][Fingers[f].Length - 1]);
                if (Root == null || Tip == null) continue;
                Sum += Vector3.Angle(HandDir, Tip.position - Root.position);
                Count++;
            }
            if (Count > 0) TrackedCurl = Sum / Count;
        }
        if (Ball == null) return;
        Transform Palm = Bone("Palm"), Wrist = Bone("Wrist");
        Transform MiddleProx = Bone("MiddleProximal"), IndexProx = Bone("IndexProximal"), LittleProx = Bone("LittleProximal");
        if (Palm == null || Wrist == null || MiddleProx == null || IndexProx == null || LittleProx == null) return;

        //手のひらの向きを骨の位置から求める（指の方向 × 横方向）
        Vector3 Normal = Vector3.Cross(MiddleProx.position - Wrist.position,
            IndexProx.position - LittleProx.position).normalized;
        if (!isRightHand) Normal = -Normal;
        //最初のフレームだけ、握っている指先がある側を手のひら側として符号を確かめる
        if (PalmSide == 0.0f)
        {
            Transform MiddleTip = Bone("MiddleTip");
            float Side = (MiddleTip != null) ? Vector3.Dot(MiddleTip.position - Palm.position, Normal) : 0.0f;
            PalmSide = (Side < -0.01f) ? -1.0f : 1.0f;
        }
        Normal *= PalmSide;

        //ボールを手のひらに乗せる
        Vector3 Center = Palm.position + Normal * (BallRadius + PalmGap);
        Ball.position = Center;

        //指をいったん開き、根元の関節から順にボールの表面に届くまで曲げる
        float Reach = BallRadius + FingerThickness;
        foreach (string[] Finger in Fingers)
        {
            for (int i = 0; i < Finger.Length - 1; i++) ResetToBindPose(Finger[i]);
            for (int i = 0; i < Finger.Length - 1; i++)
            {
                Transform Joint = Bone(Finger[i]), Next = Bone(Finger[i + 1]);
                if (Joint == null || Next == null) break;
                CurlToSurface(Joint, Next, Normal, Center, Reach);
            }
        }
    }

    //関節を手のひら側へ曲げ、次の関節がボールの表面に一番近づく角度にする
    void CurlToSurface(Transform Joint, Transform Next, Vector3 Normal, Vector3 Center, float Reach)
    {
        Vector3 Dir = Next.position - Joint.position;
        Vector3 Axis = Vector3.Cross(Dir, Normal);
        if (Axis.sqrMagnitude < 1e-8f) return;
        float BestAngle = 0.0f;
        float BestGap = float.MaxValue;
        for (float Angle = 0.0f; Angle <= MaxCurl; Angle += CurlStep)
        {
            Vector3 Pos = Joint.position + Quaternion.AngleAxis(Angle, Axis) * Dir;
            float Gap = Mathf.Abs(Vector3.Distance(Pos, Center) - Reach);
            if (Gap < BestGap)
            {
                BestGap = Gap;
                BestAngle = Angle;
            }
            if (Vector3.Distance(Pos, Center) <= Reach) break; //表面に届いた
        }
        Joint.rotation = Quaternion.AngleAxis(BestAngle, Axis) * Joint.rotation;
    }
}
