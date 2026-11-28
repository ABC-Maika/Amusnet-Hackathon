using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>Rebel Legacy: bonus carried between runs (the martyr effect). Stored in PlayerPrefs.</summary>
    public static class LegacyBank
    {
        const string Key = "RebelLegacy";
        const string DeathsKey = "RebelDeaths";

        public static float Total => PlayerPrefs.GetFloat(Key, 0f);
        public static int Deaths => PlayerPrefs.GetInt(DeathsKey, 0);

        /// <summary>Bonus multiplier applied to mess score at the start of a run (1 = none).</summary>
        public static float RunBonusMultiplier(float perLegacyPoint = 0.01f, float cap = 2f)
            => 1f + Mathf.Min(Total * perLegacyPoint, cap - 1f);

        /// <summary>Legacy multiplier from recruited allies: 1 + sum of each ally's own bonus, capped. No allies = ×1.</summary>
        public static float AllyMultiplier(float allyBonusSum, float cap = 3f)
            => Mathf.Min(1f + Mathf.Max(0f, allyBonusSum), cap);

        public static void Add(float amount)
        {
            PlayerPrefs.SetFloat(Key, Total + Mathf.Max(0f, amount));
            PlayerPrefs.SetInt(DeathsKey, Deaths + 1);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(DeathsKey);
        }
    }
}
