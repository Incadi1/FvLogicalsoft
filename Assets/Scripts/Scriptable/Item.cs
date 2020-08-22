// La estructura Elemento solo contiene las propiedades dinámicas del elemento, 
// de modo que las propiedades estáticas se pueden leer desde el objeto programable.
//
// Los elementos deben ser estructuras para poder trabajar con SyncLists.
//
// Usa .Equals para comparar dos elementos. Comparar el nombre NO es suficiente
// para los casos en que las estadísticas dinámicas difieren. 
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mirror;

[Serializable]
public partial struct Item
{
    // hashcode usado para hacer referencia al ScriptableItem real (no se puede
    // vincular a los datos directamente porque synclist solo admite tipos simples)
    // y sincronizar el código hash de astring en lugar de la cadena toma MUCHO menos 
    // ancho de banda.
    public int hash;

    // constructors
    public Item(ScriptableItem data)
    {
        hash = data.name.GetStableHashCode();
    }

    // envoltorios para un acceso más fácil
    public ScriptableItem data
    {
        get
        {
            // muestra un mensaje de error útil si no se puede encontrar la clave
            //  nota: ScriptableItem.OnValidate 'está en la carpeta de recursos' la 
            //  comprobación provoca advertencias de Unity SendMessage y falsos positivos.
            // Esta solución es mucho mejor.
            if (!ScriptableItem.dict.ContainsKey(hash))
                throw new KeyNotFoundException("No hay ScriptableItem con hash =" + hash + ".   Asegúrese de que todos los ScriptableItems estén en la carpeta Recursos para que se carguen correctamente.");
            return ScriptableItem.dict[hash];
        }
    }
    public string name => data.name;   
    public bool destroyable => data.destroyable;
    public Sprite image => data.image;

   // tooltip
    public string ToolTip()
    {
        // nota: el almacenamiento en caché de StringBuilder es peor para GC porque .Clear libera la matriz interna y la reasigna.
        StringBuilder tip = new StringBuilder(data.ToolTip());

        // addon system hooks
        Utils.InvokeMany(typeof(Item), this, "ToolTip_", tip);

        return tip.ToString();
    }

}
