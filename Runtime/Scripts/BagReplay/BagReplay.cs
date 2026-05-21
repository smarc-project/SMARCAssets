using System;
using System.IO;
using UnityEngine;

namespace BagReplay
{
    [Serializable]
    public struct FloatRange
    {
        public float start;
        public float end;

        public FloatRange(float start, float end)
        {
            this.start = start;
            this.end = end;
        }
    }

    public class BagReplay : MonoBehaviour
    {
        [SerializeField] [HideInInspector] private string filePath = string.Empty;

        [HideInInspector] public float limitStart = 0;
        [HideInInspector] public float limitEnd = 0;
        [HideInInspector] public FloatRange replayRange = new() { start = 0f, end = 0f };

        public static event Action<BagReplay> OnReplayRestart;
        public static event Action<BagReplay> OnReplayDone;

        public BagReader bagReader;
        [HideInInspector] public double currentTime = 0;
        public BagData CurrentBagData;
        public BagData NextBagData;
        public BagData PreviousBagData;


        public bool evalMode = false;
        [HideInInspector] public bool isPlaying = false;
        public bool stopTimeAtEnd;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ResetState();
                return;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"BagReplay could not find bag file at '{filePath}'.");
                ResetState();
                return;
            }

            bagReader = new BagReader(filePath);

            limitStart = 0;
            limitEnd = (float)((bagReader.EndNanos - bagReader.StartNanos) / 1000000000);

            RestartReplay();
        }

        public void RestartReplay()
        {
            if (bagReader == null)
            {
                ResetState();
                return;
            }

            currentTime = replayRange.start;
            UpdateBagData();

            isPlaying = true;
            if (Application.isPlaying) OnReplayRestart?.Invoke(this);
        }

        private void FixedUpdate()
        {
            if (!isPlaying || bagReader == null)
            {
                return;
            }

            var newTime = currentTime + Time.fixedDeltaTime;
            if (replayRange.end > 0 && newTime > replayRange.end)
            {
                CurrentBagData = new BagData();
                PreviousBagData = new BagData();
                NextBagData = new BagData();
                if (Application.isPlaying && isPlaying)
                {
                    isPlaying = false;
                    if (stopTimeAtEnd) Time.timeScale = 0;
                    OnReplayDone?.Invoke(this);
                }

                return;
            }

            currentTime = newTime;
            UpdateBagData();
        }

        private void UpdateBagData()
        {
            if (bagReader == null)
            {
                return;
            }

            var queryTime = bagReader.StartNanos + currentTime * 1000000000;

            var bagData = bagReader.ReadFields(queryTime);
            if (bagData != null)
            {
                PreviousBagData = CurrentBagData;
                CurrentBagData = bagData;
            }

            bagData = bagReader.ReadFields(queryTime + Time.fixedDeltaTime * 1000000000);
            if (bagData != null)
            {
                NextBagData = bagData;
            }
        }

        private void ResetState()
        {
            bagReader = null;
            
            limitStart = 0f;
            limitEnd = 0f;
            replayRange = new FloatRange(0f, 0f);
            currentTime = 0d;
            
            CurrentBagData = new BagData();
            NextBagData = new BagData();
            PreviousBagData = new BagData();
            
            isPlaying = false;
        }
    }
}
