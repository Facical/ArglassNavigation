using UnityEngine;

namespace ARNavExperiment.Core
{
    [System.Serializable]
    public class ParticipantSession
    {
        public string participantId;
        public string condition;   // "glass_only" | "hybrid"
        public string missionSet;  // "Set1" | "Set2"

        public ParticipantSession(string id, string cond, string missionSet)
        {
            participantId = id;
            condition = cond;
            this.missionSet = missionSet;
        }
    }
}
