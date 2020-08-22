// Note: this script has to be on an always-active UI parent, so that we can
// always react to the hotkey.

using System;
using UnityEngine;
using UnityEngine.UI;

public partial class UIAdministratorTool : MonoBehaviour
{
    public KeyCode hotKey = KeyCode.F10;
    public GameObject panel;

    [Header("Server")]
    public Text connectionsText;
    public Text maxConnectionsText;
    public Text onlinePlayerText;
    public Text uptimeText;
    public Text tickRateText;
    public InputField globalChatInput;
    public Button globalChatSendButton;
    public Button shutdownButton;

    [Header("Actions")]
    public InputField playerNameInput;
    public Button summonButton;
    public Button killButton;

    void Update()
    {
        Player player = Player.localPlayer;
        if (player != null && player.isAdministrator)
        {
            // hotkey (not while typing in chat, etc.)
            if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
                panel.SetActive(!panel.activeSelf);

            // only refresh the panel while it's active
            if (panel.activeSelf)
            {
                // SERVER PANEL /////////////////////////////////////////////
                connectionsText.text = player.administratorTool.connections.ToString();
                maxConnectionsText.text = player.administratorTool.maxConnections.ToString();
                onlinePlayerText.text = player.administratorTool.onlinePlayers.ToString();
                uptimeText.text = Utils.PrettySeconds(player.administratorTool.uptime);
                tickRateText.text = player.administratorTool.tickRate.ToString();

                // global chat
                globalChatSendButton.interactable = !string.IsNullOrWhiteSpace(globalChatInput.text);
                globalChatSendButton.onClick.SetListener(() => {
                  //  player.administratorTool.CmdSendGlobalMessage(globalChatInput.text);
                    globalChatInput.text = string.Empty;
                });

                // Apagar Servidor
                shutdownButton.onClick.SetListener(() => {
                    UIConfirmation.singleton.Show("Seguro que desea apagar le servidor?", () => {
                        player.administratorTool.CmdShutdown();
                    });
                });

                // ACTIONS PANEL ///////////////////////////////////////////////
                // convocar
                summonButton.interactable = !string.IsNullOrWhiteSpace(playerNameInput.text);
                summonButton.onClick.SetListener(() => {
                    player.administratorTool.CmdSummon(playerNameInput.text);
                });

                // kill
                killButton.interactable = !string.IsNullOrWhiteSpace(playerNameInput.text);
                killButton.onClick.SetListener(() => {
                    player.administratorTool.CmdRemove(playerNameInput.text);
                });
            }
        }
        else panel.SetActive(false);
    }
}
