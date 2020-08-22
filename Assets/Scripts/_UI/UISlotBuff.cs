// Adjunte al prefabricado para facilitar el acceso a los componentes mediante los scripts de la interfaz de usuario. 
// De lo contrario, necesitaríamos slot.GetChild (0) .GetComponentInChildren <Texto> etc.
using UnityEngine;
using UnityEngine.UI;

public class UIBuffSlot : MonoBehaviour
{
    public Image image;
    public UIShowToolTip tooltip;
    public Slider slider;
}
