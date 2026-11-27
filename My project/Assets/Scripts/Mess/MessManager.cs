using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amusnet.Mess
{
    // One scorekeeper for the run. The workspace carries it on the butterfly.
    public sealed class MessManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Positive score milestones, in points.")]
        private int[] thresholds = { 5, 15, 30 };

        public int Score { get; private set; }
        public int LastThreshold { get; private set; }
        public event Action<int> ScoreChanged;
        public event Action<int> ThresholdReached;

        private readonly Queue<int> pendingAwards = new();
        private bool dispatching;

        private void Awake()
        {
            Array.Sort(thresholds);
        }

        public void AddMess(int points)
        {
            if (points <= 0) return;

            // A chain reaction can submit another award inside an event listener.
            // Finish notifying listeners about this award before applying the next.
            pendingAwards.Enqueue(points);
            if (dispatching) return;
            dispatching = true;
            try
            {
                while (pendingAwards.Count > 0)
                {
                    int previous = Score;
                    Score = (int)Math.Min(int.MaxValue, (long)previous + pendingAwards.Dequeue());
                    if (Score == previous) continue;
                    ScoreChanged?.Invoke(Score);
                    foreach (int threshold in thresholds)
                    {
                        if (threshold > Score) break;
                        if (threshold <= previous || threshold <= LastThreshold) continue;
                        LastThreshold = threshold;
                        ThresholdReached?.Invoke(threshold);
                    }
                }
            }
            finally
            {
                dispatching = false;
            }
        }
    }
}
