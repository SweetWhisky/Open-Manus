using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RunRealms2
{
    public enum PickupKind { Shard, Shield, Magnet, Boost }
    public enum AvoidanceKind { None, Jump, Slide }

    public sealed class WorldInteractable : MonoBehaviour
    {
        public PickupKind Pickup;
        public bool IsHazard;
        public AvoidanceKind Avoidance;
        public int Value = 1;
        public bool Consumed;
    }

    public sealed class RunnerMotor : MonoBehaviour
    {
        public event Action<int> ShardCollected;
        public event Action<PickupKind> PickupCollected;
        public event Action Hit;
        public event Action NearMiss;

        public float FocusEnergy { get; private set; } = 100f;
        public bool FocusActive => _focusTimer > 0f;
        public float WorldSpeedScale => FocusActive ? 0.46f : (_boostTimer > 0f ? 1.22f : 1f);
        public bool HasShield => _shield;
        public bool MagnetActive => _magnetTimer > 0f;
        public int Lane => _lane;

        private const float LaneSpacing = 2.65f;
        private const float JumpVelocity = 8.7f;
        private const float Gravity = 25f;
        private const float LaneSharpness = 18f;

        private Rigidbody _body;
        private CapsuleCollider _collider;
        private ProceduralHumanoid _visual;
        private int _lane = 1;
        private float _verticalVelocity;
        private float _slideTimer;
        private float _focusTimer;
        private float _boostTimer;
        private float _magnetTimer;
        private bool _shield;
        private bool _acceptInput;
        private Vector2 _touchStart;
        private float _touchStartTime;
        private int _touchFinger = -1;

        public void Initialize(RealmPalette palette)
        {
            _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _body.useGravity = false;

            _collider = gameObject.AddComponent<CapsuleCollider>();
            _collider.height = 2.05f;
            _collider.radius = 0.36f;
            _collider.center = new Vector3(0f, 1.02f, 0f);
            _collider.isTrigger = true;

            _visual = ProceduralCharacterFactory.CreateHumanoid(transform, palette, true);
            transform.position = new Vector3(0f, 0f, 0f);
        }

        public void ResetRunner(RealmPalette palette)
        {
            _lane = 1;
            transform.position = Vector3.zero;
            _verticalVelocity = 0f;
            _slideTimer = 0f;
            _focusTimer = 0f;
            _boostTimer = 0f;
            _magnetTimer = 0f;
            _shield = false;
            FocusEnergy = 100f;
            _acceptInput = true;
            _visual.SetPalette(palette);
            UpdateCollider();
        }

        public void SetInputEnabled(bool enabled)
        {
            _acceptInput = enabled;
            if (!enabled) _touchFinger = -1;
        }

        public void SetPalette(RealmPalette palette) => _visual.SetPalette(palette);

        private void Update()
        {
            ReadInput();

            var dt = Time.unscaledDeltaTime;
            _focusTimer = Mathf.Max(0f, _focusTimer - dt);
            _boostTimer = Mathf.Max(0f, _boostTimer - dt);
            _magnetTimer = Mathf.Max(0f, _magnetTimer - dt);
            if (!FocusActive) FocusEnergy = Mathf.Min(100f, FocusEnergy + 14f * dt);

            var grounded = transform.position.y <= 0.001f;
            if (!grounded || _verticalVelocity > 0f)
            {
                _verticalVelocity -= Gravity * dt;
            }
            else
            {
                _verticalVelocity = 0f;
            }

            var position = transform.position;
            position.x = Mathf.Lerp(position.x, (_lane - 1) * LaneSpacing, 1f - Mathf.Exp(-LaneSharpness * dt));
            position.y = Mathf.Max(0f, position.y + _verticalVelocity * dt);
            _body.MovePosition(position);

            _slideTimer = Mathf.Max(0f, _slideTimer - dt);
            UpdateCollider();
            _visual.Tick(Time.unscaledTime, Mathf.Abs(position.x - (_lane - 1) * LaneSpacing), position.y, _slideTimer > 0f, FocusActive, _boostTimer > 0f);
        }

        private void ReadInput()
        {
            if (!_acceptInput) return;

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) ChangeLane(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) ChangeLane(1);
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)) Jump();
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Slide();
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) TriggerFocus();

            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) continue;
                    _touchFinger = touch.fingerId;
                    _touchStart = touch.position;
                    _touchStartTime = Time.unscaledTime;
                }
                else if (touch.fingerId == _touchFinger && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
                {
                    var delta = touch.position - _touchStart;
                    var duration = Time.unscaledTime - _touchStartTime;
                    HandleGesture(delta, duration);
                    _touchFinger = -1;
                }
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0) && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                _touchStart = Input.mousePosition;
                _touchStartTime = Time.unscaledTime;
            }
            if (Input.GetMouseButtonUp(0)) HandleGesture((Vector2)Input.mousePosition - _touchStart, Time.unscaledTime - _touchStartTime);
#endif
        }

        private void HandleGesture(Vector2 delta, float duration)
        {
            if (!_acceptInput) return;
            const float threshold = 44f;
            if (delta.magnitude < threshold && duration < 0.4f)
            {
                TriggerFocus();
                return;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) ChangeLane(delta.x > 0f ? 1 : -1);
            else if (delta.y > 0f) Jump();
            else Slide();
        }

        private void ChangeLane(int direction)
        {
            _lane = Mathf.Clamp(_lane + direction, 0, 2);
        }

        private void Jump()
        {
            if (transform.position.y <= 0.02f && _slideTimer <= 0f) _verticalVelocity = JumpVelocity;
        }

        private void Slide()
        {
            if (transform.position.y <= 0.08f) _slideTimer = 0.78f;
        }

        private void TriggerFocus()
        {
            if (FocusEnergy < 26f || _focusTimer > 0f) return;
            FocusEnergy -= 26f;
            _focusTimer = 1.3f;
        }

        private void UpdateCollider()
        {
            var sliding = _slideTimer > 0f;
            _collider.height = sliding ? 1.05f : 2.05f;
            _collider.center = new Vector3(0f, _collider.height * 0.5f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            var interactable = other.GetComponent<WorldInteractable>();
            if (interactable == null || interactable.Consumed) return;

            if (interactable.IsHazard)
            {
                var avoided = interactable.Avoidance switch
                {
                    AvoidanceKind.Jump => transform.position.y > 0.78f,
                    AvoidanceKind.Slide => _slideTimer > 0.05f,
                    _ => false
                };
                if (avoided)
                {
                    NearMiss?.Invoke();
                    return;
                }

                interactable.Consumed = true;
                if (_shield)
                {
                    _shield = false;
                    Hit?.Invoke();
                    return;
                }
                Hit?.Invoke();
                return;
            }

            interactable.Consumed = true;
            other.gameObject.SetActive(false);
            if (interactable.Pickup == PickupKind.Shard)
            {
                ShardCollected?.Invoke(Mathf.Max(1, interactable.Value));
                return;
            }

            switch (interactable.Pickup)
            {
                case PickupKind.Shield:
                    _shield = true;
                    break;
                case PickupKind.Magnet:
                    _magnetTimer = 8f;
                    break;
                case PickupKind.Boost:
                    _boostTimer = 4.5f;
                    break;
            }
            PickupCollected?.Invoke(interactable.Pickup);
        }
    }

    public sealed class ProceduralHumanoid : MonoBehaviour
    {
        private Transform _root;
        private Transform _torso;
        private Transform _head;
        private Transform _leftArm;
        private Transform _rightArm;
        private Transform _leftLeg;
        private Transform _rightLeg;
        private Transform _visor;
        private Renderer[] _renderers;
        private bool _player;

        public void Configure(bool player, Transform root, Transform torso, Transform head, Transform leftArm, Transform rightArm, Transform leftLeg, Transform rightLeg, Transform visor)
        {
            _player = player;
            _root = root;
            _torso = torso;
            _head = head;
            _leftArm = leftArm;
            _rightArm = rightArm;
            _leftLeg = leftLeg;
            _rightLeg = rightLeg;
            _visor = visor;
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void SetPalette(RealmPalette palette)
        {
            var suit = RuntimeAssets.Material("runner-suit-" + palette.Id, Color.Lerp(palette.Ground, Color.white, _player ? 0.48f : 0.22f), 0.35f, 0.72f);
            var accent = RuntimeAssets.Material("runner-accent-" + palette.Id, palette.Primary, 0.15f, 0.85f, true);
            foreach (var renderer in _renderers)
            {
                renderer.sharedMaterial = renderer.transform == _visor || renderer.name.Contains("Accent", StringComparison.OrdinalIgnoreCase) ? accent : suit;
            }
        }

        public void Tick(float time, float laneError, float height, bool sliding, bool focused, bool boosted)
        {
            var cadence = boosted ? 15f : focused ? 5.5f : 10.5f;
            var phase = time * cadence;
            var swing = Mathf.Sin(phase) * (_player ? 42f : 28f);
            _leftArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
            _rightArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            _leftLeg.localRotation = Quaternion.Euler(-swing * 0.72f, 0f, 0f);
            _rightLeg.localRotation = Quaternion.Euler(swing * 0.72f, 0f, 0f);
            _root.localPosition = new Vector3(0f, sliding ? -0.34f : Mathf.Abs(Mathf.Sin(phase)) * 0.055f, 0f);
            _root.localRotation = Quaternion.Euler(sliding ? 58f : -height * 3f, 0f, Mathf.Clamp(-laneError * 12f, -12f, 12f));
            _torso.localScale = Vector3.Lerp(_torso.localScale, sliding ? new Vector3(1f, 0.82f, 1.18f) : Vector3.one, 0.28f);
            _head.localRotation = Quaternion.Euler(focused ? -8f : 0f, Mathf.Sin(time * 1.8f) * 2f, 0f);
        }
    }

    public static class ProceduralCharacterFactory
    {
        public static ProceduralHumanoid CreateHumanoid(Transform parent, RealmPalette palette, bool player)
        {
            var root = new GameObject(player ? "PlayerVisual" : "CitizenVisual").transform;
            root.SetParent(parent, false);

            var torso = Part(PrimitiveType.Capsule, "Torso", root, new Vector3(0f, 1.42f, 0f), new Vector3(0.72f, 0.72f, 0.48f));
            var chest = Part(PrimitiveType.Cube, "AccentChest", torso, new Vector3(0f, 0.16f, -0.42f), new Vector3(0.72f, 0.18f, 0.08f));
            var head = Part(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 2.28f, 0f), new Vector3(0.55f, 0.62f, 0.55f));
            var visor = Part(PrimitiveType.Cube, "AccentVisor", head, new Vector3(0f, 0.02f, -0.48f), new Vector3(0.62f, 0.18f, 0.08f));
            var leftArm = Limb("LeftArm", root, new Vector3(-0.54f, 1.7f, 0f), new Vector3(0.24f, 0.68f, 0.24f));
            var rightArm = Limb("RightArm", root, new Vector3(0.54f, 1.7f, 0f), new Vector3(0.24f, 0.68f, 0.24f));
            var leftLeg = Limb("LeftLeg", root, new Vector3(-0.23f, 0.72f, 0f), new Vector3(0.3f, 0.82f, 0.3f));
            var rightLeg = Limb("RightLeg", root, new Vector3(0.23f, 0.72f, 0f), new Vector3(0.3f, 0.82f, 0.3f));

            RemoveCollider(torso.gameObject);
            RemoveCollider(chest.gameObject);
            RemoveCollider(head.gameObject);
            RemoveCollider(visor.gameObject);
            RemoveCollider(leftArm.gameObject);
            RemoveCollider(rightArm.gameObject);
            RemoveCollider(leftLeg.gameObject);
            RemoveCollider(rightLeg.gameObject);

            var humanoid = root.gameObject.AddComponent<ProceduralHumanoid>();
            humanoid.Configure(player, root, torso, head, leftArm, rightArm, leftLeg, rightLeg, visor);
            humanoid.SetPalette(palette);
            return humanoid;
        }

        private static Transform Limb(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            var pivot = new GameObject(name + "Pivot").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            var limb = Part(PrimitiveType.Capsule, name, pivot, new Vector3(0f, -scale.y * 0.45f, 0f), scale);
            return pivot;
        }

        private static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type).transform;
            part.name = name;
            part.SetParent(parent, false);
            part.localPosition = position;
            part.localScale = scale;
            return part;
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
        }
    }
}
