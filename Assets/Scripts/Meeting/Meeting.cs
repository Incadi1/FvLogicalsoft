// Las reuniones deben ser estructuras para poder trabajar con SyncLists.


public struct Meeting
{
    // Guild.Empty for ease of use
    public static Meeting Empty = new Meeting();

    // properties
    public int meetingId;
    public string[] members; // first one == master
    public bool shareExperience;
    public bool shareGold;

    // helper properties
    public string master => members != null && members.Length > 0 ? members[0] : "";

    // statics
    public static int Capacity = 8;
    public static float BonusExperiencePerMember = 0.1f;

    // if we create a meeting then always with two initial members
    public Meeting(int meetingId, string master, string firstMember)
    {
        // create members array
        this.meetingId = meetingId;
        members = new string[] { master, firstMember };
        shareExperience = false;
        shareGold = false;
    }

    public bool Contains(string memberName)
    {
        if (members != null)
            foreach (string member in members)
                if (member == memberName)
                    return true;
        return false;
    }

    public bool IsFull()
    {
        return members != null && members.Length == Capacity;
    }
}
