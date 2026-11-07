using System.Collections;
using UnityEngine;

public class TreasureChest : MonoBehaviour
{
    public int goldAmount = 20;
    private bool collected = false;
    public Animator animator;
    public float destroychest;
    public void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnCollisionEnter(Collision collision)
{
    Debug.Log("Столкновение: " + collision.gameObject.name);

    if (collected)
        return;

    Transform hit = collision.collider.transform;

    AttackController attackController =
        hit.GetComponentInParent<AttackController>();

    GettingResources gettingResources =
        hit.GetComponentInParent<GettingResources>();

    if (!FactionMember.IsPlayer(hit.gameObject) ||
        gettingResources == null)
        return;

        collected = true;
        gettingResources.Gold += goldAmount;

        Debug.Log("Юнит забрал золото из сундука");

        if (animator != null)
            animator.SetBool("isOpened", true);

StartCoroutine(ChestDestroy());
}
 IEnumerator ChestDestroy()
    {
        yield return new WaitForSeconds(destroychest);
        Destroy(gameObject);
    }
    }
