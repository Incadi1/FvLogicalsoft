using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class UIMeetingHUD : MonoBehaviour
{
    public GameObject panel;
    public UIMeetingHUDMemberSlot slotPrefab;
    public Transform memberContent;
    //[Range(0,1)] public float visiblityAlphaRange = 0.5f;
    public AnimationCurve alphaCurve;

    void Update()
    {
        Player player = Player.localPlayer;

        // only show and update while there are meeting members
        if (player != null)
        {
            if (player.meeting.InMeeting())
            {
                panel.SetActive(true);
                Meeting meeting = player.meeting.meeting;

                // get meeting members without self. no need to show self in HUD too.
                List<string> members = player.meeting.InMeeting() ? meeting.members.Where(m => m != player.name).ToList() : new List<string>();

                // instantiate/destroy enough slots
                UIUtils.BalancePrefabs(slotPrefab.gameObject, members.Count, memberContent);

                // refresh all members
                for (int i = 0; i < members.Count; ++i)
                {
                    UIMeetingHUDMemberSlot slot = memberContent.GetChild(i).GetComponent<UIMeetingHUDMemberSlot>();
                    string memberName = members[i];
                    float distance = Mathf.Infinity;
                    float visRange = player.VisRange();

                    slot.nameText.text = memberName;
                    slot.masterIndicatorText.gameObject.SetActive(meeting.master == memberName);


                    if (Player.onlinePlayers.ContainsKey(memberName))
                    {
                        Player member = Player.onlinePlayers[memberName];
                        slot.icon.sprite = member.classIcon;
                        slot.backgroundButton.onClick.SetListener(() => {
          
                            if (member != null)
                                player.CmdSetTarget(member.netIdentity);
                        });

                        // distance color based on visRange ratio
                        distance = Vector3.Distance(player.transform.position, member.transform.position);
                        visRange = member.VisRange(); // visRange is always based on the other guy
                    }

                    // distance overlay alpha based on visRange ratio
                    // (because values are only up to date for members in observer
                    //  range)
                    float ratio = visRange > 0 ? distance / visRange : 1f;
                    float alpha = alphaCurve.Evaluate(ratio);

                    // icon alpha
                    Color iconColor = slot.icon.color;
                    iconColor.a = alpha;
                    slot.icon.color = iconColor;

                }
            }
            else panel.SetActive(false);
        }
        else panel.SetActive(false);
    }
}
