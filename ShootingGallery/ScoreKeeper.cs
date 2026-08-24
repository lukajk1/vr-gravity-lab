using TMPro;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// Tracks the running score and hit count, and writes them to a world space label.
    /// Targets report into this through the static ReportHit helper so they do not need
    /// a reference wired up in the inspector.
    /// </summary>
    public class ScoreKeeper : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Label showing the current score")]
        TMP_Text m_ScoreLabel = null;

        [SerializeField]
        [Tooltip("Label showing how many targets have been hit")]
        TMP_Text m_HitsLabel = null;

        static ScoreKeeper s_Instance;

        int m_Score;
        int m_Hits;

        public int score => m_Score;
        public int hits => m_Hits;

        void Awake()
        {
            s_Instance = this;
            Refresh();
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        /// <summary>
        /// Adds points to the active scorekeeper, if one exists in the scene.
        /// </summary>
        public static void ReportHit(int points)
        {
            if (s_Instance != null)
                s_Instance.AddScore(points);
        }

        public void AddScore(int points)
        {
            m_Score += points;
            m_Hits++;
            Refresh();
        }

        public void ResetScore()
        {
            m_Score = 0;
            m_Hits = 0;
            Refresh();
        }

        void Refresh()
        {
            if (m_ScoreLabel != null)
                m_ScoreLabel.text = $"SCORE  {m_Score}";

            if (m_HitsLabel != null)
                m_HitsLabel.text = $"HITS  {m_Hits}";
        }
    }
}
