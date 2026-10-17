using UnityEngine;

public class ChangeWorldTest : MonoBehaviour
{
    public GameObject world1;
    public GameObject world2;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       world1.SetActive(true);
         world2.SetActive(false); 
    }


    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F))
        {
            world1.SetActive(!world1.activeSelf);
            world2.SetActive(!world2.activeSelf);
        }
    }
}
