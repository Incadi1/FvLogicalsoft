using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public partial class UICharacterCreation : MonoBehaviour
{
    public NetworkManagerFV manager; 
    public GameObject panel;
    public InputField nameInput;
    public Dropdown classDropdown;
    public Toggle administratorToggle;
    public Button createButton;
    public Button cancelButton;

    void Update()
    {
        if (panel.activeSelf)
        {
            if (manager.state == NetworkState.Lobby)
            {
                Show();

                classDropdown.options = manager.playerClasses.Select(
                    p => new Dropdown.OptionData(p.name)
                    ).ToList();

            
                administratorToggle.gameObject.SetActive(NetworkServer.localClientActive);

                createButton.interactable = manager.IsAllowedCharacterName(nameInput.text);
                createButton.onClick.SetListener(() => {
                    CharacterCreateMsg message = new CharacterCreateMsg
                    {
                        name = nameInput.text,
                        classIndex = classDropdown.value,
                        administrator = administratorToggle.isOn
                    };
                    Debug.Log("name character: " + name);
                    Debug.Log("class Character: " + classDropdown.value);
                    Debug.Log("administrador?: " + administratorToggle.isOn);

                    NetworkClient.Send(message);
                    Hide();
                });

                // cancel
                cancelButton.onClick.SetListener(() => {
                    nameInput.text = "";
                    Hide();
                });
            }
            else Hide();
        }
    }

    public void Hide() { panel.SetActive(false); }
    public void Show() { panel.SetActive(true); }
    public bool IsVisible() { return panel.activeSelf; }
}
