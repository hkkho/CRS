using System;

namespace ReactorSim.Game
{
    /// <summary>Authored session goals. Uses existing fuel stock, limits and scoring.</summary>
    public sealed class ShiftObjective
    {
        public const string ThreeDayId = "three-day-rhythm-v1";
        public const string StewardshipId = "seven-day-stewardship-v1";

        private ShiftObjective(string id, string title, string objective, int days, uint usefulBundles, string reward)
        { Id = id; Title = title; Objective = objective; Days = days; UsefulBundles = usefulBundles; Reward = reward; }

        public string Id { get; }
        public string Title { get; }
        public string Objective { get; }
        public int Days { get; }
        public uint UsefulBundles { get; }
        public string Reward { get; }
        public bool IsChallenge => Id != ShiftProgress.PracticeId;

        public static bool IsSupported(string id) => id == ShiftProgress.PracticeId ||
            id == ShiftProgress.ChallengeId || id == ThreeDayId || id == StewardshipId;

        public static ShiftObjective ForId(string id)
        {
            switch (id)
            {
                case ShiftProgress.PracticeId:
                    return new ShiftObjective(id, "Free practice",
                    "Keep channel powers close to their time-average reference to reduce ripple and build score.", 0, 0, "");
                case ShiftProgress.ChallengeId:
                    return new ShiftObjective(id, "One useful fuel day",
                    "Discharge 8 bundles at 6 MWd/kg or above, then finish the day with regulating headroom.", 1, 8, "Efficient refueller badge");
                case ThreeDayId:
                    return new ShiftObjective(id, "Three-day rhythm",
                    "Complete three days within the operating limits using a shared stock of 128 fresh bundles. Build score by keeping the power distribution smooth.", 3, 0, "Steady hands badge");
                case StewardshipId:
                    return new ShiftObjective(id, "Seven-day stewardship",
                    "Complete seven days within the operating limits and discharge at least 48 bundles at 6 MWd/kg or above. Your 128 fresh bundles must last the whole run.", 7, 48, "Fuel steward badge");
                default:
                    throw new ArgumentException("Unknown shift objective.", nameof(id));
            }
        }
    }
}
