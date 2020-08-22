// Sincronizar el nombre de una entidad es crucial para los componentes que necesitan 
// el nombre apropiado en la función Inicio (por ejemplo, para cargar la barra de 
//habilidades por nombre).
//
// Simplemente usar OnSerialize y OnDeserialize es la forma más fácil de hacerlo. 
// El uso de una SyncVar requeriría Inicio, Ganchos, etc.
using UnityEngine;
using Mirror;

[DisallowMultipleComponent]
public class NetworkName : NetworkBehaviourNonAlloc
{
    // server-side serialization
    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        writer.WriteString(name);
        return true;
    }

    // client-side deserialization
    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        name = reader.ReadString();
    }
}