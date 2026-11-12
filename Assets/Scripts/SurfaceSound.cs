using System.Collections;
using UnityEngine;

//ボールが当たった時に固有の音を鳴らし、少し揺れる面（リム・バックボード・支柱）
//子のコライダーに当たった場合も、親にこれが付いていれば反応する
public class SurfaceSound : MonoBehaviour
{
    [SerializeField] AudioClip Clip; //当たった音
    [SerializeField] float Volume = 1.0f; //最大音量
    [SerializeField] float Pitch = 1.0f; //音の高さ（同じ音を支柱などで低くして使い回す）
    [SerializeField] float MinSpeed = 0.6f; //これより弱く当たった時は鳴らさない
    [SerializeField] float FullSpeed = 7.0f; //この速さで当たると最大音量
    [SerializeField] Transform[] ShakeTargets; //当たった時に揺らすもの
    [SerializeField] Vector3 ShakeDir = Vector3.down; //揺れる向き（ワールド）
    [SerializeField] float ShakeAmount = 0.015f; //一番強く当たった時の揺れ幅（m）
    [SerializeField] float ShakeTime = 0.45f; //揺れが収まるまでの時間
    [SerializeField] float ShakeFreq = 11.0f; //揺れの速さ（回/秒）

    Vector3[] BasePos; //揺らす前の位置
    Coroutine Shaking;
    float LastTime = -1.0f; //最後に鳴らした時刻

    void Awake()
    {
        BasePos = new Vector3[ShakeTargets.Length];
        for (int i = 0; i < ShakeTargets.Length; i++)
        {
            if (ShakeTargets[i] != null) BasePos[i] = ShakeTargets[i].localPosition;
        }
    }

    //ボールが当たった **********************************************
    public void Hit(Vector3 Point, float Speed)
    {
        if (Speed < MinSpeed) return;
        if (Time.time - LastTime < 0.06f) return; //リムの上で転がった時に鳴りすぎないように
        LastTime = Time.time;
        float Rate = Mathf.Clamp01(Speed / FullSpeed);
        if (Clip != null) PlayAt(Point, Volume * (0.25f + 0.75f * Rate), Pitch * Random.Range(0.95f, 1.05f));
        if (ShakeTargets.Length > 0)
        {
            if (Shaking != null) StopCoroutine(Shaking);
            Shaking = StartCoroutine(ShakeRun(Rate));
        }
    }

    //ピッチを変えて鳴らす（PlayClipAtPoint は高さを変えられない）
    void PlayAt(Vector3 Pos, float Vol, float P)
    {
        GameObject Go = new GameObject("SurfaceSound");
        Go.transform.position = Pos;
        AudioSource Src = Go.AddComponent<AudioSource>();
        Src.clip = Clip;
        Src.volume = Vol;
        Src.pitch = P;
        Src.spatialBlend = 1.0f;
        Src.minDistance = 2.0f; //少し離れていてもよく聞こえるように
        Src.Play();
        Destroy(Go, Clip.length / P + 0.1f);
    }

    IEnumerator ShakeRun(float Rate)
    {
        for (float t = 0.0f; t < ShakeTime; t += Time.deltaTime)
        {
            float Amp = ShakeAmount * Rate * Mathf.Exp(-t * 7.0f) * Mathf.Sin(t * ShakeFreq * Mathf.PI * 2.0f);
            for (int i = 0; i < ShakeTargets.Length; i++)
            {
                Transform Tr = ShakeTargets[i];
                if (Tr == null) continue;
                Vector3 Dir = Tr.parent != null ? Tr.parent.InverseTransformDirection(ShakeDir) : ShakeDir;
                Tr.localPosition = BasePos[i] + Dir * Amp;
            }
            yield return null;
        }
        for (int i = 0; i < ShakeTargets.Length; i++)
        {
            if (ShakeTargets[i] != null) ShakeTargets[i].localPosition = BasePos[i];
        }
        Shaking = null;
    }
}
