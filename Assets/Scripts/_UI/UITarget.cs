// Nota: esta secuencia de comandos debe estar en un elemento primario de interfaz de usuario 
// siempre activo, para que siempre podamos encontrarlo en otro código. (GameObject.Find no encuentra los inactivos)
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public partial class UITarget : MonoBehaviour
{
    public GameObject panel;
    public Text nameText;
    public Transform buffsPanel;
    //public UIBuffSlot buffSlotPrefab;
    public Button meetingInviteButton;

    void Update()
    {
        Player player = Player.localPlayer;
        if (player != null)
        {
            Entity target = player.nextTarget ?? player.target;
            if (target != null && target != player)
            {
                float distance = Utils.ClosestDistance(player, target);

                panel.SetActive(true);
                nameText.text = target.name;

                if (target is Player targetPlayer2)
                {
                    meetingInviteButton.gameObject.SetActive(true);
                    meetingInviteButton.interactable = (!player.meeting.InMeeting() || !player.meeting.meeting.IsFull()) &&
                                                     !targetPlayer2.meeting.InMeeting() &&
                                                     NetworkTime.time >= player.nextRiskyActionTime &&
                                                     distance <= player.interactionRange;
                    meetingInviteButton.onClick.SetListener(() => {
                        player.meeting.CmdInvite(target.name);
                    });
                }
                else meetingInviteButton.gameObject.SetActive(false);
            }
            else panel.SetActive(false);
        }
        else panel.SetActive(false);
    }
}
