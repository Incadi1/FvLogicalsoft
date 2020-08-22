// Guarda la información del elemento en un ScriptableObject que se puede usar 
// en el juego haciendo referencia a él desde un MonoBehaviour. Solo almacena 
// los datos estáticos de un elemento.

// También agregamos cada uno a un diccionario automáticamente, para que todos 
// puedan ser encontrados por nombre sin tener que ponerlos a todos en una base 
// de datos. Tenga en cuenta que tenemos que ponerlos todos en la carpeta Recursos 
// y usar Resources.LoadAll para cargarlos. Esto es importante porque algunos 
// elementos pueden no estar referenciados por ninguna entidad en el juego 
// (por ejemplo, cuando un elemento de evento especial ya no se suelta después  
// del evento). Pero todos los elementos deben poder cargarse desde la base de datos, 
// incluso si ya no se hace referencia a ellos. Entonces tenemos que usar Resources.Load.
// (antes de agregarlos al dict en OnEnable, pero solo se requiere para aquellos a 
// los que se hace referencia en el juego. Todos los demás serán ignorados como Unity).
//
// Se puede crear un elemento haciendo clic derecho en la carpeta Recursos y seleccionando 
//Crear -> Elemento uMMORPG. Los elementos existentes se pueden encontrar en la carpeta
//Recursos.
//
// Nota: esta clase no es abstracta, por lo que podemos crear elementos "inútiles" 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "Feria virtual Item/General", order = 999)]
public partial class ScriptableItem : ScriptableObjectNonAlloc
{
    public bool destroyable;
    [SerializeField, TextArea(1, 30)] protected string toolTip; // not public, use ToolTip()
    public Sprite image;

    // información sobre herramientas /////////////////////////////////////////////////////////////////
    // complete todas las variables en la información sobre herramientas, esto nos ahorra un
    // montón de código de concatenación de cadenas feo. (las dinámicas se completan en Item.cs)
    // -> nota: cada información sobre herramientas puede tener cualquier variable, o ninguna 
    // si es necesario
    public virtual string ToolTip()
    {
        // nota: el almacenamiento en caché de StringBuilder es peor para GC porque. 
        // Clear libera la matriz interna y se reasigna.
        StringBuilder tip = new StringBuilder(toolTip);
        tip.Replace("{NAME}", name);
        tip.Replace("{DESTROYABLE}", (destroyable ? "Yes" : "No"));
        return tip.ToString();
    }

    // almacenamiento en caché /////////////////////////////////////////////// //////////////////
    // solo podemos usar Resources.Load en el hilo principal. no podemos usarlo al declarar 
    // variables estáticas. así que tenemos que usarlo tan pronto como se acceda a 'dict' por 
    // primera vez desde el hilo principal.
    // -> guardamos el hash para que la parte del elemento dinámico no tenga que contener y 
    // sincronizar el nombre completo en la red
    static Dictionary<int, ScriptableItem> cache;
    public static Dictionary<int, ScriptableItem> dict
    {
        get
        {
            // not loaded yet?
            if (cache == null)
            {
                // get all ScriptableItems in resources
                ScriptableItem[] items = Resources.LoadAll<ScriptableItem>("");

                // check for duplicates, then add to cache
                List<string> duplicates = items.ToList().FindDuplicates(item => item.name);
                if (duplicates.Count == 0)
                {
                    cache = items.ToDictionary(item => item.name.GetStableHashCode(), item => item);
                }
                else
                {
                    foreach (string duplicate in duplicates)
                        Debug.LogError("La carpeta de recursos contiene varios ScriptableItems con el nombre " + duplicate + ".Si está utilizando subcarpetas como 'Mujer / Anillo' y 'Hombre / Anillo', cámbieles el nombre a 'Mujer / Anillo (Mujer)' y 'Hombre / Anillo (Hombre)'.");
                }
            }
            return cache;
        }
    }
}
