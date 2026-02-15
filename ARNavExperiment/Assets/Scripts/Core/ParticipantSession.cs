using UnityEngine;

namespace ARNavExperiment.Core
{
    [System.Serializable]
    public class ParticipantSession
    {
        public string participantId;
        public string orderGroup; // "S1" (Glass→Hybrid) or "S2" (Hybrid→Glass)
        public string firstRoute;  // "A" or "B"
        public string secondRoute; // "B" or "A"

        public string FirstCondition => orderGroup == "S1" ? "glass_only" : "hybrid";
        public string SecondCondition => orderGroup == "S1" ? "hybrid" : "glass_only";

        public ParticipantSession(string id, string group)
        {
            participantId = id;
            orderGroup = group;

            int num = 0;
            if (id.Length > 1) int.TryParse(id.Substring(1), out num);

            // counterbalancing: odd participants → A first, even → B first
            firstRoute = (num % 2 == 1) ? "A" : "B";
            secondRoute = (num % 2 == 1) ? "B" : "A";
        }
    }
}
