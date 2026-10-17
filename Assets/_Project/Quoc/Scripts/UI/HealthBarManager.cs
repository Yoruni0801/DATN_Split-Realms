// using UnityEngine;

// public class EnemyHealth : MonoBehaviour
// {
//     public float maxHP = 100;
//     public float currentHP;

//     public GameObject hpBarPrefab;
//     private HealthBarUI hpBar;

//     void Start()
//     {
//         currentHP = maxHP;

//         // spawn UI vào Canvas chung
//         GameObject obj = Instantiate(hpBarPrefab, GameObject.Find("HealthBarContainer").transform);
//         hpBar = obj.GetComponent<HealthBarUI>();

//         hpBar.SetTarget(transform);
//         hpBar.SetMaxHP(maxHP);
//         hpBar.UpdateHP(currentHP);
//     }

//     public void TakeDamage(float damage)
//     {
//         currentHP -= damage;

//         hpBar.UpdateHP(currentHP);
//         hpBar.Show();

//         if (currentHP <= 0)
//         {
//             Die();
//         }
//     }

//     void Die()
//     {
//         if (hpBar != null)
//             Destroy(hpBar.gameObject);

//         Destroy(gameObject);
//     }
// }