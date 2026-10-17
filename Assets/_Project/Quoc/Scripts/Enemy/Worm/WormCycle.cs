using UnityEngine;

public class WormCycle : MonoBehaviour
{
    public float timeUnder = 2f;   // thời gian dưới đất
    public float timeAbove = 2f;   // thời gian trên mặt đất

    private Animator anim;
    private bool isUp = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        StartCoroutine(Cycle());
    }

    System.Collections.IEnumerator Cycle()
    {
        while (true)
        {
            // 🕳️ Nằm dưới đất
            isUp = false;
            yield return new WaitForSeconds(timeUnder);

            // ⬆️ Chui lên
            anim.SetTrigger("Emerge");
            yield return new WaitForSeconds(0.5f); // delay theo animation

            // 🟢 Ở trên
            isUp = true;
            yield return new WaitForSeconds(timeAbove);

            // ⬇️ Chui xuống
            anim.SetTrigger("Hide");
            yield return new WaitForSeconds(0.5f);
        }
    }

    public bool IsUp()
    {
        return isUp;
    }
}