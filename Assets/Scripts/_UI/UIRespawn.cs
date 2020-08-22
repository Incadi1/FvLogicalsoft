using UnityEngine;
using UnityEngine.UI;

public partial class UIRespawn : MonoBehaviour
{
    public GameObject panel;
    public Button button;

    void Update()
    {
        Player player = Player.localPlayer;

        // 
        if (player != null)
        {
            panel.SetActive(true);
            button.onClick.SetListener(() => { player.CmdRespawn(); });
        }
        else panel.SetActive(false);
    }
}