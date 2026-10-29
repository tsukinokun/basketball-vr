using UnityEngine;
using UnityEngine.UI; //uGUIの利用に必要

//画像や文字を平行四辺形に傾ける（スポーツ中継風の斜めのプレート用）
[RequireComponent(typeof(Graphic))]
public class UISkew : BaseMeshEffect
{
    [SerializeField] float Angle = 12.0f; //傾ける角度（度）。正で上側が右にずれる

    //コードから作った時に角度を決める
    public void SetAngle(float Value)
    {
        Angle = Value;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        float Tan = Mathf.Tan(Angle * Mathf.Deg2Rad);
        float CenterY = graphic.rectTransform.rect.center.y; //真ん中の高さは動かさない
        UIVertex V = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref V, i);
            V.position.x += (V.position.y - CenterY) * Tan;
            vh.SetUIVertex(V, i);
        }
    }
}
