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
        [SerializeField, Min(1)] private int goalScore = 100;

        public int Score { get; private set; }
        public int LastThreshold { get; private set; }
        public int GoalScore => goalScore;
        public bool HasReachedGoal => Score >= goalScore;
        public double ElapsedSeconds => goalTime ?? (startedAt.HasValue ? Time.timeAsDouble - startedAt.Value : 0d);
        public event Action<int> ScoreChanged;
        public event Action<int> ThresholdReached;
        public event Action<Interactable, int> MessCreated;

        private readonly Queue<int> pendingAwards = new();
        private bool dispatching;
        private double? startedAt;
        private double? goalTime;

        private void Awake()
        {
            Array.Sort(thresholds);
        }

        public void BeginRun()
        {
            startedAt ??= Time.timeAsDouble;
        }

        internal void RecordInteraction(Interactable source)
        {
            AddMess(source.Points);
            MessCreated?.Invoke(source, source.Points);
        }

        public void AddMess(int points)
        {
            if (points <= 0) return;
            BeginRun();

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
                    if (HasReachedGoal && !goalTime.HasValue)
                        goalTime = ElapsedSeconds;
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
