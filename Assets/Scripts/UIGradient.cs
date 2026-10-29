using UnityEngine;
using UnityEngine.UI; //uGUIの利用に必要

//画像や文字に上下のグラデーションをかける（元の色に掛け合わせる）
[RequireComponent(typeof(Graphic))]
public class UIGradient : BaseMeshEffect
{
    [SerializeField] Color Top = Color.white; //上端の色
    [SerializeField] Color Bottom = new Color(0.7f, 0.7f, 0.7f, 1.0f); //下端の色

    //コードから作った時に色を決める
    public void SetColors(Color _top, Color _bottom)
    {
        Top = _top;
        Bottom = _bottom;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        Rect R = graphic.rectTransform.rect;
        UIVertex V = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref V, i);
            float Rate = Mathf.InverseLerp(R.yMin, R.yMax, V.position.y);
            V.color = (Color32)((Color)V.color * Color.Lerp(Bottom, Top, Rate));
            vh.SetUIVertex(V, i);
        }
    }
}
