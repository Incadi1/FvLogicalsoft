using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[DisallowMultipleComponent]
public class PlayerMeeting : NetworkBehaviourNonAlloc
{
    [Header("Components")]
    public Player player;

    // .meeting es una copia para facilitar la lectura / sincronización. 
    // MeetingSystem de usuarios para gestionar reuniones!
    [Header("meeting")]
    [SyncVar, HideInInspector] public Meeting meeting; // TODO SyncToOwner later
    [SyncVar, HideInInspector] public string inviteFrom = "";
    public float inviteWaitSeconds = 3;

    void OnDestroy()
    {
        // no hacer nada si no se genera (= para las vistas previas de selección de personajes)
        if (!isServer && !isClient) return;

        // Unity bug: isServer es falso cuando se llama en modo host. solo es cierto cuando se 
        // llama en modo dedicado. entonces necesitamos una solución alternativa:
        if (NetworkServer.active) // isServer
        {
            // salir de la reunión (si hay)
            if (InMeeting())
            {
                // descartar si maestro, dejar lo contrario
                if (meeting.master == name)
                    Dismiss();
                else
                    Leave();
            }
        }

    }
    // meeting ///////////////////////////////////////////////////////////////////
    public bool InMeeting()
    {
        // 0 significa que no hay reunión, porque la reunión predeterminada de la estructura de la reunión es 0.
        return meeting.meetingId > 0;
    }

    // encuentre miembros de la reunión cercanos para compartir elementos
    public List<Player> GetMembersInProximity()
    {
        List<Player> players = new List<Player>();
        if (InMeeting())
        {
            // (evite Linq porque es PESADO (!) en GC y rendimiento)
            foreach (NetworkConnection conn in netIdentity.observers.Values)
            {
                Player observer = conn.identity.GetComponent<Player>();
                if (meeting.Contains(observer.name))
                    players.Add(observer);
            }
        }
        return players;
    }

    // invitación a la reunión por nombre (no por destino) para que los comandos
    // de chat sean posibles si es necesario
    [Command]
    public void CmdInvite(string otherName)
    {
        // validar: ¿hay alguien con ese nombre y no uno mismo?
        if (otherName != name &&
            Player.onlinePlayers.TryGetValue(otherName, out Player other) &&
            NetworkTime.time >= player.nextRiskyActionTime)
        {
            // solo puede enviar invitaciones si aún no hay una reunión o si la reunión no 
            // está llena y tiene derechos de invitación y el otro tipo aún no está en la reunión
            if ((!InMeeting() || !meeting.IsFull()) && !other.meeting.InMeeting())
            {
                // enviar una invitación
                other.meeting.inviteFrom = name;
                print(name + " invited " + other.name + " to meeting");
            }
        }

        // restablecer tiempo arriesgado pase lo que pase. incluso si la invitación falla, 
        // no queremos que los jugadores puedan enviar spam al botón de invitación e invitar 
        // a jugadores aleatorios en masa.
        player.nextRiskyActionTime = NetworkTime.time + inviteWaitSeconds;
    }

    [Command]
    public void CmdAcceptInvite()
    {
        /// invitación válida?
         // nota: no hay verificación de distancia porque el remitente podría estar muy lejos
        if (!InMeeting() && inviteFrom != "" &&
            Player.onlinePlayers.TryGetValue(inviteFrom, out Player sender))
        {
            // está en reunión? luego intenta agregar
            if (sender.meeting.InMeeting())
                MeetingSystem.AddToMeeting(sender.meeting.meeting.meetingId, name);
            // de lo contrario intenta formar uno nuevo
            else
                MeetingSystem.FormMeeting(sender.name, name);
        }

        // restablecer la invitación a la reunión en cualquier caso
        inviteFrom = "";
    }

    [Command]
    public void CmdDeclineInvite()
    {
        inviteFrom = "";
    }

    [Command]
    public void CmdKick(string member)
    {
        // intenta patear. El sistema de reuniones hará toda la validación.
        MeetingSystem.KickFromMeeting(meeting.meetingId, name, member);
    }


    // versión sin cmd porque también necesitamos llamarlo desde el servidor
    public void Leave()
    {
        // intenta irte. El sistema de reuniones hará toda la validación.
        MeetingSystem.LeaveMeeting(meeting.meetingId, name);
    }
    [Command]
    public void CmdLeave() { Leave(); }

    // versión sin cmd porque también necesitamos llamarlo desde el servidor
    public void Dismiss()
    {
        // intenta descartar. El sistema de reuniones hará toda la validación.
        MeetingSystem.DismissMeeting(meeting.meetingId, name);
    }
    [Command]
    public void CmdDismiss() { Dismiss(); }
}
