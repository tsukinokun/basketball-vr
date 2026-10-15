using UnityEngine;

public class ItemAction : MonoBehaviour
{
    [SerializeField] float DestroyLevel = -5.0f; //撤去される高さ
    [SerializeField] float ThrownPower = 20.0f; //投てき力
    [SerializeField] float EffectLife = 2.5f; //エフェクトの寿命
    [SerializeField] float ForceLevel = 180.0f; //破壊判定衝撃値
    [SerializeField] GameObject Effect; //エフェクト
    Renderer myRenderer; //自身のレンダラー
    Rigidbody myRB; //自身の物理挙動

    GameObject Manager; //マネージャー
    void Start()
    {
        if (Effect != null)
        {
            Effect.SetActive(false); //無効化
        }
        //自身のレンダラーを取得
        myRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        myRB = GetComponent<Rigidbody>(); //自身の物理挙動を取得
        ChangeColor(Color.white); //白に変色
    }

    //マネージャー設定処理 ******************
    void SetManager(GameObject _manager)
    {
        Manager = _manager;
    }

    //何かに接触時の処理 ******************************************
    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.tag == "Item") return;
        //衝撃力を算出
        float impactForce = other.impulse.magnitude / Time.fixedDeltaTime;
        Debug.Log("Force : " + impactForce);
        //衝撃力が規定値以上なら破壊処置
        if (impactForce > ForceLevel)
        {
            if (Manager != null)
            {
                //マネージャーに破壊位置を通知
                Manager.SendMessage("ItemCrash", transform.position,
                SendMessageOptions.DontRequireReceiver);
            }

            Effect.transform.parent = null; //エフェクトと親子関係を切る
            Effect.SetActive(true); //エフェクト実体化
            Destroy(Effect, EffectLife); //エフェクトに寿命を与える
            Destroy(gameObject); //アイテム自身は即撤去
        }
    }

    //掴まれる処理（true : 掴む／false : 放す）********************************************
    void Grabed(bool Mode)
    {
        //物理挙動を一斉に制限する
        myRB.constraints = Mode ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.None;
        myRB.useGravity = !Mode; //重力利用の可否
        ChangeColor(Mode ? Color.red : Color.green);
    }

    //色変更処理 **********************************************
    void ChangeColor(Color ColorName)
    {
        myRenderer.material.color = ColorName;
    }

    //投てき処理 **********************************************
    void Thrown(Vector3 Dir)
    {
        //引数の「投てきベクトルDir」へ自身を撃ち出す
        myRB.AddForce(Dir * ThrownPower, ForceMode.Impulse);
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
