using UnityEngine.UI;
using UnityEngine;

public class healthBarBehavior : MonoBehaviour
{

    public Image filledPart;

    public void ShowHealthFraction(float fraction)
    {
       filledPart.rectTransform.localScale = new Vector3(fraction, 1, 1); 
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        


    }
}
