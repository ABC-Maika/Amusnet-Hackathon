using System.Collections.Generic;
using System.Globalization;
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

        // ---- where previous butterflies died (spirit trail guides the next run there) ----
        const string PointsKey = "RebelDeathPoints";
        const int MaxPoints = 8;

        /// <summary>Oldest first.</summary>
        public static List<Vector3> GetDeathPoints()
        {
            var list = new List<Vector3>();
            string raw = PlayerPrefs.GetString(PointsKey, "");
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var entry in raw.Split(';'))
            {
                var p = entry.Split(',');
                if (p.Length == 3
                    && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                    && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    list.Add(new Vector3(x, y, z));
            }
            return list;
        }

        public static void RecordDeathPoint(Vector3 point)
        {
            var list = GetDeathPoints();
            list.Add(point);
            while (list.Count > MaxPoints) list.RemoveAt(0);
            var parts = new List<string>();
            foreach (var v in list)
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", v.x, v.y, v.z));
            PlayerPrefs.SetString(PointsKey, string.Join(";", parts));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(DeathsKey);
            PlayerPrefs.DeleteKey(PointsKey);
        }
    }
}
