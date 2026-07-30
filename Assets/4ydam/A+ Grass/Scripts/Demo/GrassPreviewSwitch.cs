using UnityEngine;
using UnityEngine.UI;

public class GrassPreviewSwitch : MonoBehaviour
{
    [SerializeField] private Text setNameText;
    [SerializeField] private GameObject[] grassSets;
    private int currentSetIndex = 0;

    private void Start()
    {
        ShowCurrentGrassSet();
    }
    
    public void SwitchGrassSet(int direction)
    {
        currentSetIndex = (currentSetIndex + direction + grassSets.Length) % grassSets.Length;
        ShowCurrentGrassSet();
    }

    private void ShowCurrentGrassSet()
    {
        for (int i = 0; i < grassSets.Length; i++)
        {
            grassSets[i].SetActive(i == currentSetIndex);
            setNameText.text = grassSets[currentSetIndex].name;
        }
    }

}
