using UnityEngine;

[DisallowMultipleComponent]
public class ZombieRandomSkinSelector : MonoBehaviour
{
    [Header("Skins")]
    [SerializeField] private GameObject[] skins;
    [SerializeField] private bool avoidImmediateRepeat = true;

    private int lastSelectedIndex = -1;

    private void OnEnable()
    {
        ApplyRandomSkin();
    }

    public void ApplyRandomSkin()
    {
        if (skins == null || skins.Length == 0)
        {
            return;
        }

        int selectedIndex = Random.Range(0, skins.Length);
        if (avoidImmediateRepeat && skins.Length > 1 && selectedIndex == lastSelectedIndex)
        {
            selectedIndex = (selectedIndex + 1) % skins.Length;
        }

        for (int i = 0; i < skins.Length; i++)
        {
            GameObject skin = skins[i];
            if (skin == null)
            {
                continue;
            }

            skin.SetActive(i == selectedIndex);
        }

        lastSelectedIndex = selectedIndex;
    }
}
