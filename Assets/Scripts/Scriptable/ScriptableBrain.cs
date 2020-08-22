// Las máquinas de estados finitos de entidad son programables. De esta manera, es muy fácil
// modificar los comportamientos jugadores sin tocar el código central.
// -> también permite múltiples tipos de IA de NPC, lo que siempre es bueno tener.
//
// ScriptableBrains tiene una arquitectura completamente abierta. Puede usar otros ScriptableObjects 
// para estados, puede usar otros activos de IA, editores visuales, etc. siempre que herede de 
// ScriptableBrain y sobrescriba las dos funciones de actualización.
// -> esta es la solución más simple y más abierta.
using UnityEngine;

public abstract class ScriptableBrain : ScriptableObjectNonAlloc
{
    // actualiza la máquina de estado del servidor, devuelve el siguiente estado
    public abstract string UpdateServer(Entity entity);

    // actualiza la máquina de estado del cliente
    public abstract void UpdateClient(Entity entity);

    // DrawGizmos se puede usar para mostrar información de depuración
    // (no se puede nombrar "On" DrawGizmos, de lo contrario, Unity se queja de los parámetros)
    public virtual void DrawGizmos(Entity entity) { }
}