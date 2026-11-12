using UnityEngine;
using UnityEngine.UI; //uGUIの利用に必要

//別のテキストの内容をそのまま写す（アリーナのスコアボードに得点・残り時間を出す）
[RequireComponent(typeof(Text))]
public class TextMirror : MonoBehaviour
{
    [SerializeField] Text Source; //写す元
    Text Me;

    void Awake()
    {
        Me = GetComponent<Text>();
    }

    void LateUpdate()
    {
        if (Source != null && Me.text != Source.text) Me.text = Source.text;
    }
}
