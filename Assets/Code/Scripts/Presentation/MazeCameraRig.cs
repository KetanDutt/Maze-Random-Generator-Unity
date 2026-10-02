using Maze.Gameplay;
using Maze.Generation;
using UnityEngine;

namespace Maze.Presentation
{
    /// <summary>Camera behaviours offered by the demo.</summary>
    public enum MazeCameraMode
    {
        /// <summary>Classic first person view following the player.</summary>
        FirstPerson = 0,

        /// <summary>Bird's eye view of the maze; drag to orbit, scroll to zoom.</summary>
        Orbit = 1,

        /// <summary>Free flying spectator camera (WASD + QE, hold the right mouse button to look).</summary>
        FreeFly = 2,
    }

    /// <summary>
    /// Drives the single camera of the scene. Depending on <see cref="Mode"/> it either follows the
    /// player, orbits around the maze or flies freely.
    /// </summary>
    [AddComponentMenu("Maze/Presentation/Maze Camera Rig")]
    public sealed class MazeCameraRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Camera that is driven by the rig. Defaults to the camera on this object.")]
        private Camera _camera;

        [SerializeField]
        [Tooltip("Maze the orbit camera looks at. Found automatically when left empty.")]
        private MazeGenerator _generator;

        [SerializeField]
        [Tooltip("Player used in first person mode. Found automatically when left empty.")]
        private MazePlayerController _player;

        [Header("Mode")]
        [SerializeField]
        private MazeCameraMode _mode = MazeCameraMode.FirstPerson;

        [SerializeField, Range(30f, 110f), Tooltip("Camera field of view.")]
        private float _fieldOfView = 70f;

        [Header("Orbit")]
        [SerializeField, Range(5f, 500f)]
        private float _orbitDistance = 70f;

        [SerializeField, Range(0f, 360f)]
        private float _orbitYaw = 45f;

        [SerializeField, Range(5f, 89f)]
        private float _orbitPitch = 55f;

        [SerializeField, Range(1f, 400f), Tooltip("Closest zoom of the orbit camera.")]
        private float _orbitMinDistance = 10f;

        [SerializeField, Range(5f, 1000f), Tooltip("Farthest zoom of the orbit camera.")]
        private float _orbitMaxDistance = 400f;

        [SerializeField, Range(0.5f, 15f)]
        private float _orbitSensitivity = 3.5f;

        [SerializeField, Range(1f, 100f)]
        private float _zoomSpeed = 25f;

        [SerializeField, Tooltip("Automatically frame the whole maze after every generation.")]
        private bool _frameOnGeneration = true;

        [Header("Free fly")]
        [SerializeField, Range(1f, 200f)]
        private float _flySpeed = 30f;

        [SerializeField, Range(1f, 10f)]
        private float _flyBoost = 4f;

        [SerializeField, Range(0.5f, 10f)]
        private float _flySensitivity = 2.4f;

        private Camera _resolvedCamera;
        private float _flyYaw;
        private float _flyPitch;
        private bool _initialised;

        /// <summary>Active camera behaviour.</summary>
        public MazeCameraMode Mode
        {
            get { return _mode; }
            set { SetMode(value); }
        }

        /// <summary>The camera this rig drives.</summary>
        public Camera Camera
        {
            get { return ResolveCamera(); }
        }

        // ---------------------------------------------------------------------
        // Unity messages
        // ---------------------------------------------------------------------

        private void Awake()
        {
            ResolveCamera();
            if (_generator == null)
            {
                _generator = FindObjectOfType<MazeGenerator>();
            }

            if (_player == null)
            {
                _player = FindObjectOfType<MazePlayerController>();
            }
        }

        private void OnEnable()
        {
            if (_generator != null)
            {
                _generator.Generated += OnMazeGenerated;
            }

            ApplyModeConstraints();
        }

        private void OnDisable()
        {
            if (_generator != null)
            {
                _generator.Generated -= OnMazeGenerated;
            }
        }

        private void Start()
        {
            InitializeCamera();
            if (_generator != null && _generator.HasResult)
            {
                OnMazeGenerated(_generator.Result);
            }
        }

        private void LateUpdate()
        {
            switch (_mode)
            {
                case MazeCameraMode.FirstPerson:
                    if (_player == null)
                    {
                        SetMode(MazeCameraMode.Orbit);
                        return;
                    }

                    Camera camera = ResolveCamera();
                    if (camera == null)
                    {
                        return;
                    }

                    camera.transform.position = _player.EyePosition;
                    camera.transform.rotation = _player.LookRotation;
                    break;

                case MazeCameraMode.Orbit:
                    UpdateOrbit();
                    break;

                default:
                    UpdateFreeFly();
                    break;
            }
        }

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>Switches to a specific mode.</summary>
        public void SetMode(MazeCameraMode mode)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            ApplyModeConstraints();
        }

        /// <summary>Cycles through the three modes.</summary>
        public MazeCameraMode CycleMode()
        {
            SetMode((MazeCameraMode)(((int)_mode + 1) % 3));
            return _mode;
        }

        /// <summary>Positions the orbit camera so the whole maze is visible.</summary>
        public void FrameMaze(MazeResult result)
        {
            if (result == null)
            {
                return;
            }

            Vector3 center = ResolveMazeCenter(result);
            float extent = Mathf.Max(result.WorldSize.x, result.WorldSize.z) * 0.5f;
            _orbitDistance = Mathf.Clamp(extent / Mathf.Tan(Mathf.Deg2Rad * 0.5f * _fieldOfView) * 1.15f,
                _orbitMinDistance, _orbitMaxDistance);

            Camera camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            camera.transform.position = OrbitPosition(center);
            camera.transform.rotation = Quaternion.LookRotation((center - camera.transform.position).normalized, Vector3.up);
        }

        // ---------------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------------

        private void OnMazeGenerated(MazeResult result)
        {
            InitializeCamera();
            ApplyModeConstraints();

            if (_frameOnGeneration)
            {
                FrameMaze(result);
            }
        }

        private void InitializeCamera()
        {
            Camera camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            camera.fieldOfView = _fieldOfView;
            if (!_initialised)
            {
                _flyYaw = camera.transform.eulerAngles.y;
                _flyPitch = 0f;
                _initialised = true;
            }
        }

        private Camera ResolveCamera()
        {
            if (_camera != null)
            {
                _resolvedCamera = _camera;
                return _resolvedCamera;
            }

            if (_resolvedCamera == null)
            {
                _resolvedCamera = GetComponent<Camera>();
            }

            if (_resolvedCamera == null)
            {
                _resolvedCamera = Camera.main;
            }

            return _resolvedCamera;
        }

        private void ApplyModeConstraints()
        {
            if (_player != null)
            {
                // The player only drives its own look rotation in first person; the rig owns the camera.
                _player.LookEnabled = _mode == MazeCameraMode.FirstPerson;
            }
        }

        private Vector3 ResolveMazeCenter(MazeResult result)
        {
            Transform root = _generator != null ? _generator.transform : transform;
            return root.TransformPoint(result.WorldCenter);
        }

        private Vector3 OrbitPosition(Vector3 center)
        {
            Quaternion rotation = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
            return center - rotation * Vector3.forward * _orbitDistance;
        }

        private void UpdateOrbit()
        {
            Camera camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            if (Input.GetMouseButton(0) && !MazeInputGateway.PointerOverUi)
            {
                _orbitYaw += Input.GetAxis("Mouse X") * _orbitSensitivity;
                _orbitPitch = Mathf.Clamp(_orbitPitch - Input.GetAxis("Mouse Y") * _orbitSensitivity, 5f, 89f);
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f && !MazeInputGateway.PointerOverUi)
            {
                _orbitDistance = Mathf.Clamp(_orbitDistance * (1f - scroll * _zoomSpeed * 0.1f),
                    _orbitMinDistance, _orbitMaxDistance);
            }

            Vector3 center = _generator != null && _generator.HasResult
                ? ResolveMazeCenter(_generator.Result)
                : Vector3.zero;

            camera.transform.position = OrbitPosition(center);
            camera.transform.rotation = Quaternion.LookRotation((center - camera.transform.position).normalized, Vector3.up);
        }

        private void UpdateFreeFly()
        {
            Camera camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            Transform cameraTransform = camera.transform;

            if (Input.GetMouseButton(1) && !MazeInputGateway.PointerOverUi)
            {
                _flyYaw += Input.GetAxis("Mouse X") * _flySensitivity;
                _flyPitch = Mathf.Clamp(_flyPitch - Input.GetAxis("Mouse Y") * _flySensitivity, -89f, 89f);
            }

            cameraTransform.rotation = Quaternion.Euler(_flyPitch, _flyYaw, 0f);

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f && !MazeInputGateway.PointerOverUi)
            {
                _flySpeed = Mathf.Clamp(_flySpeed * (1f + scroll), 1f, 300f);
            }

            if (MazeInputGateway.UiBlocksGameplay)
            {
                return;
            }

            float speed = _flySpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                speed *= _flyBoost;
            }

            Vector3 direction = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) direction += cameraTransform.forward;
            if (Input.GetKey(KeyCode.S)) direction -= cameraTransform.forward;
            if (Input.GetKey(KeyCode.A)) direction -= cameraTransform.right;
            if (Input.GetKey(KeyCode.D)) direction += cameraTransform.right;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) direction += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) direction -= Vector3.up;

            if (direction.sqrMagnitude > 0f)
            {
                cameraTransform.position += direction.normalized * (speed * Time.deltaTime);
            }
        }
    }
}
