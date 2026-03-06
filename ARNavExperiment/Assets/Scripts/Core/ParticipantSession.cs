using UnityEngine;

namespace ARNavExperiment.Core
{
    [System.Serializable]
    public class ParticipantSession
    {
        public string participantId;
        public string condition;  // "glass_only" | "hybrid"
        public string route;      // "A" | "B"

        public ParticipantSession(string id, string cond, string rt)
        {
            participantId = id;
            condition = cond;
            route = rt;
        }
    }
}
