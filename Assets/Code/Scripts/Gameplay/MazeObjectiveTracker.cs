using System;
using Maze.Generation;
using Maze.Presentation;
using UnityEngine;
using UnityEngine.Events;

namespace Maze.Gameplay
{
    /// <summary>
    /// Turns a generated maze into a small game: reach the exit. Tracks the distance to the current
    /// objective, fires events when the player arrives and exposes the numbers the HUD displays.
    /// </summary>
    [AddComponentMenu("Maze/Gameplay/Maze Objective Tracker")]
    public sealed class MazeObjectiveTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Maze that provides the objective. Found automatically when left empty.")]
        private MazeGenerator _generator;

        [SerializeField]
        [Tooltip("Player used to measure the distance. Found automatically when left empty.")]
        private MazePlayerController _player;

        [SerializeField]
        [Tooltip("Optional solution path renderer; shown by the 'hint' toggle and hidden on arrival.")]
        private MazePathRenderer _pathRenderer;

        [Header("Objective")]
        [SerializeField, Range(0.5f, 30f), Tooltip("Distance at which the objective counts as reached.")]
        private float _reachRadius = 3f;

        [SerializeField, Tooltip("Hide the solution path again as soon as the objective is reached.")]
        private bool _hidePathWhenReached = true;

        [SerializeField]
        [Tooltip("Raised every time the player reaches the objective.")]
        private UnityEvent _onObjectiveReached = new UnityEvent();

        [SerializeField]
        [Tooltip("Raised whenever a new maze provides a new objective.")]
        private UnityEvent _onObjectiveChanged = new UnityEvent();

        /// <summary>Raised every time the player reaches an objective.</summary>
        public event Action Reached;

        /// <summary>Raised when the objective of the maze changes (new generation).</summary>
        public event Action<MazeResult> ObjectiveChanged;

        /// <summary>Number of objectives reached since the component was created.</summary>
        public int CompletedCount { get; private set; }

        /// <summary>World position of the objective.</summary>
        public Vector3 ObjectiveWorldPosition { get; private set; }

        /// <summary>Planar distance from the player to the objective.</summary>
        public float Distance { get; private set; }

        /// <summary><c>true</c> when the player has reached the current objective.</summary>
        public bool IsReached { get; private set; }

        /// <summary><c>true</c> when a maze with an objective exists.</summary>
        public bool HasObjective { get; private set; }

        /// <summary>Straight line distance from the entrance to the exit of the current maze.</summary>
        public float StraightLineDistance { get; private set; }

        /// <summary>Number of cells of the solution path (0 when unknown).</summary>
        public int SolutionLength { get; private set; }

        /// <summary>The solution path renderer, if one is wired up.</summary>
        public MazePathRenderer PathRenderer
        {
            get { return _pathRenderer; }
        }

        /// <summary>Shows or hides the solution path hint.</summary>
        public void SetHintVisible(bool visible)
        {
            if (_pathRenderer != null)
            {
                _pathRenderer.SetVisible(visible);
            }
        }

        /// <summary>Toggles the solution path hint and returns the new state.</summary>
        public bool ToggleHint()
        {
            if (_pathRenderer == null)
            {
                return false;
            }

            bool visible = !_pathRenderer.IsVisible;
            _pathRenderer.SetVisible(visible);
            return visible;
        }

        private void Awake()
        {
            if (_generator == null)
            {
                _generator = FindObjectOfType<MazeGenerator>();
            }

            if (_player == null)
            {
                _player = FindObjectOfType<MazePlayerController>();
            }

            if (_pathRenderer == null)
            {
                _pathRenderer = FindObjectOfType<MazePathRenderer>();
            }
        }

        private void OnEnable()
        {
            if (_generator != null)
            {
                _generator.Generated += OnMazeGenerated;
                if (_generator.HasResult)
                {
                    OnMazeGenerated(_generator.Result);
                }
            }
        }

        private void OnDisable()
        {
            if (_generator != null)
            {
                _generator.Generated -= OnMazeGenerated;
            }
        }

        private void Update()
        {
            if (!HasObjective || _player == null)
            {
                return;
            }

            Vector3 difference = ObjectiveWorldPosition - _player.transform.position;
            difference.y = 0f;
            Distance = difference.magnitude;

            if (IsReached || Distance > _reachRadius)
            {
                return;
            }

            IsReached = true;
            CompletedCount++;

            if (_hidePathWhenReached)
            {
                SetHintVisible(false);
            }

            UnityEvent reachedEvent = _onObjectiveReached;
            if (reachedEvent != null)
            {
                reachedEvent.Invoke();
            }

            Action handler = Reached;
            if (handler != null)
            {
                handler();
            }
        }

        private void OnMazeGenerated(MazeResult result)
        {
            if (result == null)
            {
                HasObjective = false;
                return;
            }

            Transform root = _generator != null ? _generator.transform : transform;
            ObjectiveWorldPosition = root.TransformPoint(result.ExitPosition);
            HasObjective = true;
            IsReached = false;
            StraightLineDistance = Vector3.Distance(root.TransformPoint(result.SpawnPosition), ObjectiveWorldPosition);
            SolutionLength = result.Statistics != null ? result.Statistics.SolutionPathLength : 0;

            if (_player != null)
            {
                Vector3 difference = ObjectiveWorldPosition - _player.transform.position;
                difference.y = 0f;
                Distance = difference.magnitude;
            }

            UnityEvent changedEvent = _onObjectiveChanged;
            if (changedEvent != null)
            {
                changedEvent.Invoke();
            }

            Action<MazeResult> handler = ObjectiveChanged;
            if (handler != null)
            {
                handler(result);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasObjective)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.3f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(ObjectiveWorldPosition, _reachRadius);
            Gizmos.DrawLine(ObjectiveWorldPosition, ObjectiveWorldPosition + Vector3.up * 3f);
        }
    }
}
