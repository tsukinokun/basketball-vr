using UnityEngine;

//リングのすぐ下に置くトリガー。上から落ちてきたボールだけをゴールとして数える
public class GoalTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        BallAction Ball = other.GetComponentInParent<BallAction>();
        if (Ball == null) return;
        Rigidbody RB = other.attachedRigidbody;
        //下から突き上げたボールは無効
        if (RB == null || RB.isKinematic) return;
        if (RB.linearVelocity.y >= 0.0f)
        {
            Ball.Disqualify(); //下から入ったボールは、落ちて戻ってきても無効
            return;
        }
        Ball.Scored();
    }
}
