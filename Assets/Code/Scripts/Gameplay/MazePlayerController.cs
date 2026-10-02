using Maze.Generation;
using UnityEngine;

namespace Maze.Gameplay
{
    /// <summary>
    /// First person explorer for a generated maze: walk, sprint, jump, mouse look and an automatic
    /// respawn when the player falls out of the world.
    /// </summary>
    /// <remarks>
    /// The controller moves the player object; the camera is positioned by
    /// <c>MazeCameraRig</c>, which reads <see cref="EyePosition"/> and <see cref="LookRotation"/>.
    /// That split keeps a single camera in the scene and lets the rig switch between first person,
    /// orbit and free fly without enabling/disabling cameras.
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Maze/Gameplay/Maze Player Controller")]
    public sealed class MazePlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Maze the player explores. Found automatically when left empty.")]
        private MazeGenerator _generator;

        [Header("Movement")]
        [SerializeField, Range(1f, 30f), Tooltip("Walking speed in world units per second.")]
        private float _moveSpeed = 6f;

        [SerializeField, Range(1f, 4f), Tooltip("Speed multiplier while sprinting.")]
        private float _sprintMultiplier = 1.7f;

        [SerializeField, Range(5f, 200f), Tooltip("How quickly the player reaches full speed.")]
        private float _acceleration = 45f;

        [SerializeField, Range(0f, 5f), Tooltip("Jump height in world units.")]
        private float _jumpHeight = 1.2f;

        [SerializeField, Tooltip("Gravity applied to the player.")]
        private float _gravity = -22f;

        [Header("Camera")]
        [SerializeField, Range(1f, 12f), Tooltip("Mouse sensitivity.")]
        private float _mouseSensitivity = 2.4f;

        [SerializeField, Range(0f, 20f), Tooltip("Eye height above the feet.")]
        private float _eyeHeight = 1.65f;

        [SerializeField, Tooltip("Invert the vertical mouse axis.")]
        private bool _invertLook;

        [SerializeField, Tooltip("Subtle camera bobbing while walking.")]
        private bool _headBob = true;

        [SerializeField, Range(0f, 0.2f)]
        private float _headBobAmplitude = 0.045f;

        [SerializeField, Range(0.1f, 5f)]
        private float _headBobFrequency = 1.6f;

        [Header("Safety")]
        [SerializeField, Tooltip("Height below which the player is teleported back to the spawn point.")]
        private float _fallLimit = -25f;

        [SerializeField, Tooltip("Lock the mouse cursor while looking around (first person only).")]
        private bool _lockCursor = true;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;
        private float _bobPhase;
        private Vector3 _spawnPosition;
        private bool _hasSpawn;

        /// <summary>Mouse look and cursor lock are only active in first person mode.</summary>
        public bool LookEnabled { get; set; } = true;

        /// <summary>When false the controller ignores every input (used while a menu is open).</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Current pitch of the head in degrees.</summary>
        public float Pitch
        {
            get { return _pitch; }
        }

        /// <summary>World position of the eyes, including head bobbing.</summary>
        public Vector3 EyePosition
        {
            get { return transform.position + Vector3.up * (_eyeHeight + CurrentBobOffset); }
        }

        /// <summary>Rotation the camera should have, matching the body yaw and the head pitch.</summary>
        public Quaternion LookRotation
        {
            get { return Quaternion.Euler(_pitch, transform.eulerAngles.y, 0f); }
        }

        /// <summary>Horizontal speed in world units per second.</summary>
        public float HorizontalSpeed
        {
            get { return _horizontalVelocity.magnitude; }
        }

        /// <summary>Normalised horizontal speed (0 = standing, 1 = full walking speed).</summary>
        public float SpeedFactor
        {
            get { return _moveSpeed <= 0f ? 0f : Mathf.Clamp01(_horizontalVelocity.magnitude / _moveSpeed); }
        }

        /// <summary><c>true</c> while the character controller touches the ground.</summary>
        public bool IsGrounded
        {
            get { return _controller != null && _controller.isGrounded; }
        }

        /// <summary>Current vertical velocity.</summary>
        public float VerticalVelocity
        {
            get { return _verticalVelocity; }
        }

        private float CurrentBobOffset
        {
            get
            {
                if (!_headBob || _headBobAmplitude <= 0f)
                {
                    return 0f;
                }

                return Mathf.Sin(_bobPhase * Mathf.PI * 2f) * _headBobAmplitude * SpeedFactor;
            }
        }

        // ---------------------------------------------------------------------
        // Unity messages
        // ---------------------------------------------------------------------

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_generator == null)
            {
                _generator = FindObjectOfType<MazeGenerator>();
            }
        }

        private void OnEnable()
        {
            if (_generator != null)
            {
                _generator.Generated += OnMazeGenerated;
            }

            if (_generator != null && _generator.HasResult)
            {
                OnMazeGenerated(_generator.Result);
            }
        }

        private void OnDisable()
        {
            if (_generator != null)
            {
                _generator.Generated -= OnMazeGenerated;
            }

            if (_lockCursor)
            {
                SetCursorLocked(false);
            }
        }

        private void Start()
        {
            if (_lockCursor && LookEnabled)
            {
                SetCursorLocked(true);
            }
        }

        private void Update()
        {
            bool uiBlocks = MazeInputGateway.UiBlocksGameplay;
            bool canLook = LookEnabled && InputEnabled && !uiBlocks;

            HandleCursor(canLook);

            if (!InputEnabled || uiBlocks)
            {
                DampHorizontalVelocity();
                ApplyGravityAndMove(false);
                return;
            }

            if (canLook)
            {
                ApplyMouseLook();
            }

            ApplyMovement();

            if (transform.position.y < _fallLimit)
            {
                Respawn();
            }
        }

        /// <summary>Teleports the player to the current spawn point.</summary>
        public void Respawn()
        {
            if (!_hasSpawn)
            {
                return;
            }

            Teleport(_spawnPosition, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        }

        /// <summary>Teleports the player to a world position.</summary>
        public void Teleport(Vector3 worldPosition, Quaternion rotation)
        {
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            transform.position = worldPosition;
            transform.rotation = rotation;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _pitch = 0f;

            if (_controller != null)
            {
                _controller.enabled = true;
            }
        }

        /// <summary>Locks or unlocks the mouse cursor.</summary>
        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>Places the player at the entrance of a freshly generated maze.</summary>
        public void MoveToSpawn(MazeResult result)
        {
            if (result == null)
            {
                return;
            }

            Transform root = _generator != null ? _generator.transform : transform;
            Vector3 worldPosition = root.TransformPoint(result.SpawnPosition);
            Quaternion worldRotation = root.rotation * result.SpawnRotation;

            _spawnPosition = worldPosition;
            _hasSpawn = true;
            Teleport(worldPosition, worldRotation);
        }

        // ---------------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------------

        private void OnMazeGenerated(MazeResult result)
        {
            MoveToSpawn(result);
        }

        private void HandleCursor(bool canLook)
        {
            if (!_lockCursor)
            {
                return;
            }

            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (!canLook)
            {
                if (locked)
                {
                    SetCursorLocked(false);
                }

                return;
            }

            if (!locked && !MazeInputGateway.PointerOverUi && Input.GetMouseButtonDown(0))
            {
                SetCursorLocked(true);
            }
        }

        private void ApplyMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            float sensitivity = _mouseSensitivity * 2.2f;
            float yaw = Input.GetAxisRaw("Mouse X") * sensitivity;
            float pitch = Input.GetAxisRaw("Mouse Y") * sensitivity * (_invertLook ? 1f : -1f);

            _pitch = Mathf.Clamp(_pitch + pitch, -89f, 89f);
            transform.Rotate(0f, yaw, 0f, Space.Self);
        }

        private void ApplyMovement()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            bool sprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float targetSpeed = _moveSpeed * (sprinting ? _sprintMultiplier : 1f);
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * targetSpeed;

            _horizontalVelocity.x = Mathf.MoveTowards(_horizontalVelocity.x, wish.x, _acceleration * Time.deltaTime);
            _horizontalVelocity.z = Mathf.MoveTowards(_horizontalVelocity.z, wish.z, _acceleration * Time.deltaTime);

            _bobPhase += _horizontalVelocity.magnitude * Time.deltaTime * _headBobFrequency * 0.35f;
            if (_bobPhase > 1000f)
            {
                _bobPhase = 0f;
            }

            bool jump = Input.GetButtonDown("Jump");
            ApplyGravityAndMove(jump);
        }

        private void DampHorizontalVelocity()
        {
            _horizontalVelocity.x = Mathf.MoveTowards(_horizontalVelocity.x, 0f, _acceleration * Time.deltaTime);
            _horizontalVelocity.z = Mathf.MoveTowards(_horizontalVelocity.z, 0f, _acceleration * Time.deltaTime);
        }

        private void ApplyGravityAndMove(bool jump)
        {
            if (_controller == null)
            {
                return;
            }

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }

                if (jump && _jumpHeight > 0f)
                {
                    _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                }
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            Vector3 motion = new Vector3(_horizontalVelocity.x, _verticalVelocity, _horizontalVelocity.z);
            _controller.Move(motion * Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(_hasSpawn ? _spawnPosition : transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * _eyeHeight);

            if (_fallLimit > -24f)
            {
                Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.4f);
                Gizmos.DrawLine(new Vector3(-500f, _fallLimit, 0f), new Vector3(500f, _fallLimit, 0f));
            }
        }
    }
}
