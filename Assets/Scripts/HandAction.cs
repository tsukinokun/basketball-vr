using UnityEngine;

public class HandAction : MonoBehaviour
{
    [SerializeField] GameObject VisibleBall; //見える手の球
    [SerializeField] GameObject Manager; //マネージャー
    [SerializeField] float ThrownJudge = 0.5f; //投げたと判定する手の速さ（m/s）
    [SerializeField] float VelocityWindow = 0.08f; //手の速度を測る時間幅（秒）
    [SerializeField] float ThrowBoostVR = 1.0f; //投げる力の倍率（VR）
    [SerializeField] float ThrowBoostPC = 1.6f; //投げる力の倍率（PC）
    const int HistorySize = 32;
    Vector3[] PosHistory = new Vector3[HistorySize]; //手のワールド座標の履歴
    float[] TimeHistory = new float[HistorySize]; //その時刻
    int HistoryHead = 0;
    GameObject Item = null; //侵入中のボール
    bool isGrabed = false; //物体を掴んでいるかの真偽値

    void Start()
    {
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

    void Update()
    {
        RecordPosition();
        if (!Item)
        {
            isGrabed = false;
            return; //ボールに侵入していないなら以降の処理は不要
        }
        //右手トリガーかSpaceを押したら掴む
        if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger) ||
        Input.GetKeyDown(KeyCode.Space))
        {
            Item.transform.parent = transform; //ボールの親を右手にする
            Item.gameObject.SendMessage("Grabed", true,
            SendMessageOptions.DontRequireReceiver); //掴んでいる指示(true)
            isGrabed = true;
        }
        //右手トリガーかSpaceを離したら投げる
        if (isGrabed && (OVRInput.GetUp(OVRInput.RawButton.RIndexTrigger) ||
        Input.GetKeyUp(KeyCode.Space)))
        {
            Item.transform.parent = null; //ボールの親を無しにする
            Item.gameObject.SendMessage("Grabed", false,
            SendMessageOptions.DontRequireReceiver); //掴んでいない指示(false)
            isGrabed = false;

            //離した瞬間の手の速度
            Vector3 Velocity = HandVelocity();
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
            }
            Item = null; //投げたボールはもう掴まない
            if (VRAction.isVR)
            {
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch); //振動停止
            }
            return;
        }
        //VRコントローラーのバイブレーション
        if (VRAction.isVR)
        {
            OVRInput.SetControllerVibration(0f, isGrabed ? 1f : 0f, OVRInput.Controller.RTouch);
        }
    }
}
