using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GravityLab
{
    /// <summary>
    /// Global switch for the force arrows and readouts. Normally they appear only while an
    /// object is held; turning this on shows them for every object at once, which turns the
    /// scene into a live diagram of the whole field.
    /// </summary>
    /// <remarks>
    /// A static flag rather than a per-object reference, because every visualiser and label in
    /// the scene has to answer the same question every frame and there may be dozens of them.
    /// The event lets a button's label follow the state without polling.
    /// </remarks>
    public class ForceVectorDisplayMode : MonoBehaviour
    {
        static readonly List<ForceVectorDisplayMode> s_Instances = new List<ForceVectorDisplayMode>();

        /// <summary>
        /// Whether every object should show its vectors, not just the one being held.
        /// </summary>
        public static bool showAll { get; private set; }

        /// <summary>Raised on any instance whenever the shared state changes.</summary>
        public static event System.Action<bool> showAllChanged;

        [SerializeField]
        [Tooltip("State to apply when the scene loads. Off means vectors appear only while an object is held.")]
        bool m_StartEnabled;

        [SerializeField]
        [Tooltip("Raised whenever the state changes, with the new state. Wire a label here.")]
        UnityEvent<bool> m_OnToggled;

        /// <summary>Raised whenever the state changes, with the new state.</summary>
        public UnityEvent<bool> onToggled => m_OnToggled;

        /// <summary>Mirrors <see cref="showAll"/>, so a label can bind to this like any toggle.</summary>
        public bool isOn => showAll;

        void OnEnable()
        {
            s_Instances.Add(this);
        }

        void OnDisable()
        {
            s_Instances.Remove(this);

            // Leaving play mode with the flag set would carry into the next session, since
            // statics outlive the scene.
            if (s_Instances.Count == 0)
                showAll = false;
        }

        void Start()
        {
            // Applied in Start so listeners wired in the inspector exist to hear it.
            SetEnabled(m_StartEnabled);
        }

        /// <summary>
        /// Flips between showing every object's vectors and only the held one's. Wire a
        /// button's select event here.
        /// </summary>
        public void Toggle()
        {
            SetEnabled(!showAll);
        }

        /// <summary>
        /// Switches the display mode to an explicit state.
        /// </summary>
        public void SetEnabled(bool value)
        {
            showAll = value;
            showAllChanged?.Invoke(value);

            // Every instance raises its own event, so each button's label stays in step even
            // when several buttons drive the same shared flag.
            foreach (var instance in s_Instances)
            {
                if (instance != null)
                    instance.m_OnToggled?.Invoke(value);
            }
        }
    }
}
