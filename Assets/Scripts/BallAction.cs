using UnityEngine;

public class BallAction : MonoBehaviour
{
    [SerializeField] float DestroyLevel = -5.0f; //撤去される高さ
    [SerializeField] float BallLife = 8.0f; //投げてから撤去されるまでの時間
    [SerializeField] Color BaseColor = new Color(0.9f, 0.4f, 0.1f); //通常色
    [SerializeField] AudioClip SE_Bounce; //バウンド音
    [SerializeField] AudioClip SE_Catch; //キャッチ音
    [SerializeField] float BounceMinSpeed = 1.0f; //バウンド音を鳴らす最低速度
    [SerializeField] float DragCoefficient = 0.0128f; //空気抵抗の係数（N/(m/s)²）
    Renderer myRenderer; //自身のレンダラー
    Rigidbody myRB; //自身の物理挙動

    GameObject Manager; //マネージャー
    public bool IsWaiting { get; private set; } = true; //手に取られるのを待っているか
    public bool IsScored { get; private set; } = false; //ゴール済みか

    void Awake()
    {
        myRenderer = GetComponentInChildren<Renderer>(); //自身のレンダラーを取得
        myRB = GetComponent<Rigidbody>(); //自身の物理挙動を取得
        myRB.isKinematic = true; //取られるまでは空中に浮かせておく
        //ボール同士は当たらないようにする（出現位置で重なっても弾かれない）
        Collider myCol = GetComponent<Collider>();
        foreach (BallAction Other in FindObjectsByType<BallAction>(FindObjectsSortMode.None))
        {
            if (Other != this)
            {
                Physics.IgnoreCollision(myCol, Other.GetComponent<Collider>());
            }
        }
    }

    void Start()
    {
        ChangeColor(Color.white); //通常色に
    }

    //マネージャー設定処理 ******************
    void SetManager(GameObject _manager)
    {
        Manager = _manager;
    }

    //ゴール無効の報告 ******************
    public void Disqualify()
    {
        IsScored = true; //以降このボールでは得点しない
    }

    //ゴール判定からの報告 ******************
    public void Scored()
    {
        if (IsScored) return; //1つのボールで得点するのは1回だけ
        IsScored = true;
        if (Manager != null)
        {
            Manager.SendMessage("Goal", transform.position,
            SendMessageOptions.DontRequireReceiver);
        }
    }

    //掴まれる処理（true : 掴む／false : 離す）********************************************
    void Grabed(bool Mode)
    {
        if (Mode && IsWaiting)
        {
            IsWaiting = false; //手に取られた
        }
        if (Mode && SE_Catch != null)
        {
            AudioSource.PlayClipAtPoint(SE_Catch, transform.position); //キャッチ音
        }
        if (Mode)
        {
            //掴んでいる間は物理補間を切り、手の位置にそのまま追従させる（補間が残ると手とずれて震える）
            myRB.interpolation = RigidbodyInterpolation.None;
            myRB.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            myRB.isKinematic = true;
        }
        else
        {
            myRB.isKinematic = false;
            myRB.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            myRB.interpolation = RigidbodyInterpolation.Interpolate; //飛んでいる間は滑らかに
        }
        if (!Mode)
        {
            Destroy(gameObject, BallLife); //離したら一定時間後に撤去
        }
        ChangeColor(Color.white); //手が見えるので、掴んでも投げても本来の色のまま
    }

    //色変更処理（白は通常色として扱う） **********************************************
    void ChangeColor(Color ColorName)
    {
        myRenderer.material.color = (ColorName == Color.white) ? BaseColor
        : Color.Lerp(BaseColor, ColorName, 0.5f);
    }

    //投げた処理 **********************************************
    void Thrown(Vector3 Velocity)
    {
        //手の速度をそのままボールに与える
        myRB.linearVelocity = Velocity;
    }

    //回転を与える処理（手首のスナップでバックスピン） **********************************************
    void Spin(Vector3 AngularVelocity)
    {
        myRB.maxAngularVelocity = 50.0f; //既定値(7rad/s)だとスナップの回転が頭打ちになる
        myRB.angularVelocity = AngularVelocity;
    }

    //衝突処理（バウンド音） **********************************************
    void OnCollisionEnter(Collision collision)
    {
        float Speed = collision.relativeVelocity.magnitude;
        if (SE_Bounce == null || Speed < BounceMinSpeed) return;
        float Volume = Mathf.Clamp01(Speed / 8.0f); //強く当たるほど大きな音
        AudioSource.PlayClipAtPoint(SE_Bounce, transform.position, Volume);
    }

    //空気抵抗（速さの2乗に比例）。バスケットボールの値：空気密度1.2kg/m³、抗力係数0.47、断面積π×0.12²
    void FixedUpdate()
    {
        if (myRB.isKinematic) return;
        Vector3 V = myRB.linearVelocity;
        myRB.AddForce(-DragCoefficient * V.magnitude * V); //0.5×密度×抗力係数×断面積 = 約0.0128
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.position.y < DestroyLevel)
        {
            Destroy(gameObject); //規定値以下になったら撤去
        }
    }
}
