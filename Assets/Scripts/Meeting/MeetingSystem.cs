// hay diferentes formas de implementar un sistema de reunión:
//
// - Player.cs puede tener una reunión '[SyncVar]' y transmitirla a todos los 
//   miembros de la reunión cuando algo cambia en la reunión. No hay una sola fuente 
//   de verdad, lo que hace que esto sea un poco extraño. funciona, pero solo hasta que
//   necesitemos una lista global de reuniones, p. para instancias de mazmorras.
//
// - Player.cs puede tener una referencia de clase de reunión que todos los miembros comparten.
//   Mirror solo puede serializar estructuras, lo que dificulta la sincronización. También está 
//   la cuestión de nulo vs. no nulo y tendríamos que no solo patear / abandonar Reuniones,
//   sino también nunca olvidar establecer .meeting en nulo. Esto resulta en una gran cantidad 
//   de código complicado.
//
// - MeetingSystem podría tener una lista de clases de Meeting. Pero entonces el cliente 
//   necesitaría tener una clase local de SyncedMeeting, lo que hace que el acceso .meeting en
//   el servidor y el cliente sea diferente (y, por lo tanto, muy difícil).
//
// - MeetingSystem podría controlar las reuniones. Cuando se cambia algo, establece 
//   automáticamente la "reunión [SyncVar]" de cada miembro, que Mirror sincroniza 
//   automáticamente. El servidor y el cliente pueden acceder a Player.meeting para leer 
//   cualquier cosa y usar MeetingSystem para modificar las reuniones.
//
// => Esta parece ser la mejor solución para un sistema de reunión con Mirror.
// => MeetingSystem es casi independiente de Unity. Es solo un sistema de reunión con nombres
//    e ID de reunión.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MeetingSystem
{
    static Dictionary<int, Meeting> meetings = new Dictionary<int, Meeting>();

    // start meetingIds at 1. 0 means no meeting, because default meeting struct's
    // meetingId is 0.
    static int nextMeetingId = 1;

    // copy meeting to someone
    static void BroadcastTo(string member, Meeting meeting)
    {
        if (Player.onlinePlayers.TryGetValue(member, out Player player))
            player.meeting.meeting = meeting;
    }

    // copy meeting to all members & save in dictionary
    static void BroadcastChanges(Meeting meeting)
    {
        foreach (string member in meeting.members)
            BroadcastTo(member, meeting);

        meetings[meeting.meetingId] = meeting;
    }

    // check if a meetingId exists
    public static bool MeetingExists(int meetingId)
    {
        return meetings.ContainsKey(meetingId);
    }

    // creating a meeting requires at least two members. it's not a meeting if
    // someone is alone in it.
    public static void FormMeeting(string creator, string firstMember)
    {
        // create meeting
        int meetingId = nextMeetingId++;
        Meeting meeting = new Meeting(meetingId, creator, firstMember);

        // broadcast and save in dict
        BroadcastChanges(meeting);
        Debug.Log(creator + " formed a new meeting with " + firstMember);
    }

    public static void AddToMeeting(int meetingId, string member)
    {
        // meeting exists and not full?
        Meeting meeting;
        if (meetings.TryGetValue(meetingId, out meeting) && !meeting.IsFull())
        {
            // add to members
            Array.Resize(ref meeting.members, meeting.members.Length + 1);
            meeting.members[meeting.members.Length - 1] = member;

            // broadcast and save in dict
            BroadcastChanges(meeting);
            Debug.Log(member + " was added to meeting " + meetingId);
        }
    }

    public static void KickFromMeeting(int meetingId, string requester, string member)
    {
        // meeting exists?
        Meeting meeting;
        if (meetings.TryGetValue(meetingId, out meeting))
        {
            // requester is meeting master, member is in meeting, not same?
            if (meeting.master == requester && meeting.Contains(member) && requester != member)
            {
                // reuse the leave function
                LeaveMeeting(meetingId, member);
            }
        }
    }

    public static void LeaveMeeting(int meetingId, string member)
    {
        // meeting exists?
        Meeting meeting;
        if (meetings.TryGetValue(meetingId, out meeting))
        {
            // requester is not master but is in meeting?
            if (meeting.master != member && meeting.Contains(member))
            {
                // remove from list
                meeting.members = meeting.members.Where(name => name != member).ToArray();

                // still > 1 people?
                if (meeting.members.Length > 1)
                {
                    // broadcast and save in dict
                    BroadcastChanges(meeting);
                    BroadcastTo(member, Meeting.Empty); // clear for kicked person
                }
                // otherwise remove meeting. no point in having only 1 member.
                else
                {
                    // broadcast and remove from dict
                    BroadcastTo(meeting.members[0], Meeting.Empty); // clear for master
                    BroadcastTo(member, Meeting.Empty); // clear for kicked person
                    meetings.Remove(meetingId);
                }

                Debug.Log(member + " left the meeting");
            }
        }
    }

    public static void DismissMeeting(int meetingId, string requester)
    {
        // meeting exists?
        Meeting meeting;
        if (meetings.TryGetValue(meetingId, out meeting))
        {
            // is master?
            if (meeting.master == requester)
            {
                // clear meeting for everyone
                foreach (string member in meeting.members)
                    BroadcastTo(member, Meeting.Empty);

                // remove from dict
                meetings.Remove(meetingId);
                Debug.Log(requester + " dismissed the meeting");
            }
        }
    }
}