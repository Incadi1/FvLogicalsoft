// Nota: esta secuencia de comandos debe estar en un elemento primario de interfaz de 
// usuario siempre activo, para que siempre podamos encontrarlo en otro código. 
// (GameObject.Find no encuentra los inactivos)
using UnityEngine;
using UnityEngine.UI;

public partial class UIMeetingInvite : MonoBehaviour
{
    public GameObject panel;
    public Text nameText;
    public Button acceptButton;
    public Button declineButton;

    void Update()
    {
        Player player = Player.localPlayer;

        if (player != null)
        {
            if (player.meeting.inviteFrom != "")
            {
                panel.SetActive(true);
                nameText.text = player.meeting.inviteFrom;
                acceptButton.onClick.SetListener(() => {
                    player.meeting.CmdAcceptInvite();
                });
                declineButton.onClick.SetListener(() => {
                    player.meeting.CmdDeclineInvite();
                });
            }
            else panel.SetActive(false);
        }
        else panel.SetActive(false); 
    }
}
