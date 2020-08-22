using UnityEngine;
using UnityEngine.UI;

public partial class UIMeeting : MonoBehaviour
{
    public KeyCode hotKey = KeyCode.P;
    public GameObject panel;
    public Text currentCapacityText;
    public Text maximumCapacityText;
    public UIMeetingMemberSlot slotPrefab;
    public Transform memberContent;


    void Update()
    {
        Player player = Player.localPlayer;
        if (player)
        {
            // hotkey (not while typing in chat, etc.)
            if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
                panel.SetActive(!panel.activeSelf);

            // only update the panel if it's active
            if (panel.activeSelf)
            {
                Meeting meeting = player.meeting.meeting;
                int memberCount = meeting.members != null ? meeting.members.Length : 0;

                // properties
                currentCapacityText.text = memberCount.ToString();
                maximumCapacityText.text = Meeting.Capacity.ToString();

                // instantiate/destroy enough slots
                UIUtils.BalancePrefabs(slotPrefab.gameObject, memberCount, memberContent);

                // refresh all members
                for (int i = 0; i < memberCount; ++i)
                {
                    UIMeetingMemberSlot slot = memberContent.GetChild(i).GetComponent<UIMeetingMemberSlot>();
                    string memberName = meeting.members[i];

                    slot.nameText.text = memberName;
                    slot.masterIndicatorText.gameObject.SetActive(i == 0);

                    // meeting struct doesn't sync health, mana, level, etc. We find
                    // those from observers instead. Saves bandwidth and is good
                    // enough since another member's health is only really important
                    // to use when we are fighting the same monsters.
                    // => null if member not in observer range, in which case health
                    //    bars etc. should be grayed out!

                    // update some data only if around. otherwise keep previous data.
                    // update icon only if around. otherwise keep previous one.
                    if (Player.onlinePlayers.ContainsKey(memberName))
                    {
                        Player member = Player.onlinePlayers[memberName];
                        slot.icon.sprite = member.classIcon;
                    }

                    // action button:
                    // dismiss: if i=0 and member=self and master
                    // kick: if i > 0 and player=master
                    // leave: if member=self and not master
                    if (memberName == player.name && i == 0)
                    {
                        slot.actionButton.gameObject.SetActive(true);
                        slot.actionButton.GetComponentInChildren<Text>().text = "Dismiss";
                        slot.actionButton.onClick.SetListener(() => {
                            player.meeting.CmdDismiss();
                        });
                    }
                    else if (memberName == player.name && i > 0)
                    {
                        slot.actionButton.gameObject.SetActive(true);
                        slot.actionButton.GetComponentInChildren<Text>().text = "Leave";
                        slot.actionButton.onClick.SetListener(() => {
                            player.meeting.CmdLeave();
                        });
                    }
                    else
                    {
                        slot.actionButton.gameObject.SetActive(false);
                    }
                }
            }
        }
        else panel.SetActive(false);
    }
}
