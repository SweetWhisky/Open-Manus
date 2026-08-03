using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace RunRealms2
{
    public sealed class RemasterBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<RemasterGame>() != null) return;
            var root = new GameObject("RUN_REALMS_2_REMASTER");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.AddComponent<RemasterGame>();
        }
    }

    public sealed class RemasterGame : MonoBehaviour
    {
        public string DirectorStatus => _director != null ? _director.Status : "INITIALIZING DIRECTOR";

        private RunnerMotor _runner;
        private ProceduralWorld _world;
        private WorldDirectorClient _director;
        private InteractiveAudio _audio;
        private RemasterHud _hud;
        private RunnerCameraRig _cameraRig;
        private RealmLightingRig _lighting;
        private string _seed = "RUN-REALMS-2";
        private bool _running;
        private bool _paused;
        private bool _loading;
        private bool _audioEnabled = true;
        private bool _shieldArmed;
        private int _score;
        private int _shards;
        private int _nearMisses;
        private float _combo = 1f;
        private float _comboTimer;
        private float _lastDistance;
        private float _frameAccumulator;
        private int _frameSamples;
        private float _qualityCooldown;
        private RemasterQuality _quality = RemasterQuality.Balanced;

        private void Awake()
        {
            Application.runInBackground = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            QualitySettings.vSyncCount = 0;
            Physics.reuseCollisionCallbacks = true;

            BuildScene();
            StartCoroutine(InitializeServices());
            SetQuality(RemasterQuality.Balanced);
        }

        private void BuildScene()
        {
            var cameraObject = new GameObject("RemasterCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RuntimeAssets.Palettes[0].Sky;
            camera.fieldOfView = 64f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 340f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            _cameraRig = cameraObject.AddComponent<RunnerCameraRig>();

            var lightRoot = new GameObject("RealmLighting");
            lightRoot.transform.SetParent(transform, false);
            _lighting = lightRoot.AddComponent<RealmLightingRig>();
            _lighting.Initialize(RuntimeAssets.Palettes[0]);

            var runnerObject = new GameObject("Runner");
            runnerObject.transform.SetParent(transform, false);
            _runner = runnerObject.AddComponent<RunnerMotor>();
            _runner.Initialize(RuntimeAssets.Palettes[0]);
            _runner.SetInputEnabled(false);
            _cameraRig.Initialize(_runner);

            var worldObject = new GameObject("ProceduralWorld");
            worldObject.transform.SetParent(transform, false);
            _world = worldObject.AddComponent<ProceduralWorld>();
            _world.Initialize(_runner);
            _world.SetRunning(false);

            var services = new GameObject("Services");
            services.transform.SetParent(transform, false);
            _director = services.AddComponent<WorldDirectorClient>();
            _audio = services.AddComponent<InteractiveAudio>();
            _audio.Initialize();
            _hud = services.AddComponent<RemasterHud>();
            _hud.Initialize(this, _audio);

            _runner.ShardCollected += OnShard;
            _runner.PickupCollected += OnPickup;
            _runner.Hit += OnHit;
            _runner.NearMiss += OnNearMiss;
            _world.RealmChanged += OnRealmChanged;
        }

        private IEnumerator InitializeServices()
        {
            yield return _director.Initialize();
            _hud.SetDirectorStatus(_director.Status);
        }

        private void Update()
        {
            TrackPerformance();
            if (!_running || _paused || _loading) return;

            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _world.Tick(dt);
            var deltaDistance = Mathf.Max(0f, _world.Distance - _lastDistance);
            _lastDistance = _world.Distance;
            _score += Mathf.RoundToInt(deltaDistance * 10f * _combo);

            _comboTimer -= dt;
            if (_comboTimer <= 0f) _combo = Mathf.MoveTowards(_combo, 1f, dt * 0.48f);
            _cameraRig.SetMotion(_world.Speed, _runner.FocusActive, _runner.WorldSpeedScale > 1.1f);
            _hud.Tick(_score, _world.Distance, _shards, _world.CurrentPalette.Name, _runner.FocusEnergy, _combo);
        }

        public void RequestRun(string seed)
        {
            if (_loading) return;
            _seed = SanitizeSeed(seed);
            StartCoroutine(PrepareRun(_seed));
        }

        public void RequestDailyRun()
        {
            var utc = DateTime.UtcNow;
            RequestRun($"DAILY-{utc:yyyy-MM-dd}");
        }

        private IEnumerator PrepareRun(string seed)
        {
            _loading = true;
            _hud.SetDirectorStatus("BUILDING 3D WORLD...");
            WorldBlueprint blueprint = null;
            var server = false;
            yield return _director.GetBlueprint(seed, Mathf.Clamp01(PlayerPrefs.GetInt("rr2_level", 1) / 30f), (result, online) =>
            {
                blueprint = result;
                server = online;
            });

            BeginRun(seed, blueprint ?? WorldBlueprint.Local(seed));
            _hud.SetDirectorStatus(server ? "SERVER-DIRECTED WORLD" : "OFFLINE PROCEDURAL WORLD");
            _loading = false;
        }

        private void BeginRun(string seed, WorldBlueprint blueprint)
        {
            _seed = seed;
            _score = 0;
            _shards = 0;
            _nearMisses = 0;
            _combo = 1f;
            _comboTimer = 0f;
            _lastDistance = 0f;
            _shieldArmed = false;
            _paused = false;
            _running = true;
            _runner.ResetRunner(RuntimeAssets.Palettes[blueprint.realmOrder[0]]);
            _runner.SetInputEnabled(true);
            _world.BeginRun(seed, blueprint);
            _world.SetRunning(true);
            _audio.SetRealm(_world.CurrentPalette);
            _audio.SetPaused(false);
            _lighting.SetRealm(_world.CurrentPalette);
            _cameraRig.Snap();
            _hud.ShowRunning();
        }

        public void Pause()
        {
            if (!_running || _paused) return;
            _paused = true;
            _world.SetRunning(false);
            _runner.SetInputEnabled(false);
            _audio.SetPaused(true);
            _hud.ShowPaused();
        }

        public void Resume()
        {
            if (!_running || !_paused) return;
            _paused = false;
            _world.SetRunning(true);
            _runner.SetInputEnabled(true);
            _audio.SetPaused(false);
            _hud.HidePaused();
        }

        public void Restart()
        {
            _paused = false;
            RequestRun(_seed);
        }

        public void QuitToHome()
        {
            _running = false;
            _paused = false;
            _world.SetRunning(false);
            _runner.SetInputEnabled(false);
            _audio.SetPaused(false);
            _hud.ShowHome();
        }

        public void SetQuality(RemasterQuality quality)
        {
            _quality = quality;
            switch (quality)
            {
                case RemasterQuality.Performance:
                    Application.targetFrameRate = 60;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.shadowDistance = 38f;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.lodBias = 0.75f;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                    ScalableBufferManager.ResizeBuffers(0.78f, 0.78f);
                    break;
                case RemasterQuality.Ultra:
                    Application.targetFrameRate = 120;
                    QualitySettings.antiAliasing = 4;
                    QualitySettings.shadowDistance = 95f;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.lodBias = 1.55f;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                    ScalableBufferManager.ResizeBuffers(1f, 1f);
                    break;
                default:
                    Application.targetFrameRate = 90;
                    QualitySettings.antiAliasing = 2;
                    QualitySettings.shadowDistance = 62f;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.lodBias = 1.05f;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                    ScalableBufferManager.ResizeBuffers(0.9f, 0.9f);
                    break;
            }
            PlayerPrefs.SetInt("rr2_quality", (int)quality);
        }

        public void ToggleAudio()
        {
            _audioEnabled = !_audioEnabled;
            _audio.SetMasterVolume(_audioEnabled ? 1f : 0f);
        }

        private void OnShard(int value)
        {
            _shards += value;
            _score += Mathf.RoundToInt(145f * _combo) * value;
            IncreaseCombo(0.24f);
            _audio.PlayShard();
        }

        private void OnPickup(PickupKind kind)
        {
            if (kind == PickupKind.Shield) _shieldArmed = true;
            _score += Mathf.RoundToInt(420f * _combo);
            IncreaseCombo(0.55f);
            _audio.PlayPickup(kind);
        }

        private void OnNearMiss()
        {
            _nearMisses++;
            _score += Mathf.RoundToInt(240f * _combo);
            IncreaseCombo(0.34f);
            _audio.PlayNearMiss();
        }

        private void OnHit()
        {
            _audio.PlayHit();
            if (_shieldArmed)
            {
                _shieldArmed = false;
                _combo = Mathf.Max(1f, _combo * 0.55f);
                _comboTimer = 0f;
                return;
            }
            EndRun();
        }

        private void OnRealmChanged(RealmPalette palette)
        {
            _audio.SetRealm(palette);
            _lighting.SetRealm(palette);
            _cameraRig.RealmPulse(palette);
            _score += Mathf.RoundToInt(1200f * _combo);
        }

        private void IncreaseCombo(float amount)
        {
            _combo = Mathf.Min(12f, _combo + amount);
            _comboTimer = 2.2f;
        }

        private void EndRun()
        {
            if (!_running) return;
            _running = false;
            _world.SetRunning(false);
            _runner.SetInputEnabled(false);
            var best = Mathf.Max(PlayerPrefs.GetInt("rr2_best", 0), _score);
            PlayerPrefs.SetInt("rr2_best", best);
            PlayerPrefs.SetInt("rr2_shards", PlayerPrefs.GetInt("rr2_shards", 0) + _shards);
            PlayerPrefs.SetInt("rr2_level", 1 + Mathf.FloorToInt(Mathf.Sqrt(PlayerPrefs.GetInt("rr2_shards", 0) / 40f)));
            PlayerPrefs.Save();
            _hud.ShowResults(_score, _world.Distance, _shards, _nearMisses, _seed);
        }

        private void TrackPerformance()
        {
            _frameAccumulator += Time.unscaledDeltaTime;
            _frameSamples++;
            _qualityCooldown = Mathf.Max(0f, _qualityCooldown - Time.unscaledDeltaTime);
            if (_frameAccumulator < 3f) return;
            var fps = _frameSamples / Mathf.Max(0.01f, _frameAccumulator);
            _frameAccumulator = 0f;
            _frameSamples = 0;
            if (_qualityCooldown > 0f) return;

            if (_quality == RemasterQuality.Ultra && fps < 72f)
            {
                SetQuality(RemasterQuality.Balanced);
                _qualityCooldown = 8f;
            }
            else if (_quality == RemasterQuality.Balanced && fps < 48f)
            {
                SetQuality(RemasterQuality.Performance);
                _qualityCooldown = 8f;
            }
        }

        private static string SanitizeSeed(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "RUN-REALMS-2";
            var source = input.Trim().ToUpperInvariant();
            var buffer = new char[Mathf.Min(32, source.Length)];
            var count = 0;
            foreach (var character in source)
            {
                if (!(char.IsLetterOrDigit(character) || character == '-')) continue;
                buffer[count++] = character;
                if (count == buffer.Length) break;
            }
            return count == 0 ? "RUN-REALMS-2" : new string(buffer, 0, count);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) Pause();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Pause();
        }
    }

    public sealed class RunnerCameraRig : MonoBehaviour
    {
        private RunnerMotor _runner;
        private Camera _camera;
        private float _speed;
        private bool _focus;
        private bool _boost;
        private Color _pulseColor;
        private float _pulse;

        public void Initialize(RunnerMotor runner)
        {
            _runner = runner;
            _camera = GetComponent<Camera>();
            Snap();
        }

        public void SetMotion(float speed, bool focus, bool boost)
        {
            _speed = speed;
            _focus = focus;
            _boost = boost;
        }

        public void Snap()
        {
            transform.position = new Vector3(0f, 4.25f, -7.6f);
            transform.LookAt(new Vector3(0f, 1.25f, 7f));
        }

        public void RealmPulse(RealmPalette palette)
        {
            _pulseColor = palette.Primary;
            _pulse = 1f;
        }

        private void LateUpdate()
        {
            if (_runner == null) return;
            var dt = Time.unscaledDeltaTime;
            var desired = new Vector3(_runner.transform.position.x * 0.28f, 4.15f + _runner.transform.position.y * 0.12f, -7.6f);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-6.5f * dt));
            var target = new Vector3(_runner.transform.position.x * 0.18f, 1.25f + _runner.transform.position.y * 0.18f, 7.4f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target - transform.position), 1f - Mathf.Exp(-8f * dt));
            var desiredFov = _focus ? 56f : _boost ? 76f : Mathf.Lerp(63f, 69f, Mathf.InverseLerp(14f, 32f, _speed));
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, desiredFov, 1f - Mathf.Exp(-5f * dt));
            _pulse = Mathf.Max(0f, _pulse - dt * 1.6f);
            if (_pulse > 0f) _camera.backgroundColor = Color.Lerp(_camera.backgroundColor, _pulseColor, _pulse * 0.06f);
        }
    }

    public sealed class RealmLightingRig : MonoBehaviour
    {
        private Light _key;
        private Light _rim;
        private Light _portal;

        public void Initialize(RealmPalette palette)
        {
            _key = Light("KeyLight", LightType.Directional, 1.12f, 120f);
            _key.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            _key.shadows = LightShadows.Soft;
            _rim = Light("RimLight", LightType.Directional, 0.55f, 80f);
            _rim.transform.rotation = Quaternion.Euler(28f, 145f, 0f);
            _portal = Light("PortalGlow", LightType.Point, 4.5f, 26f);
            _portal.transform.position = new Vector3(0f, 3.5f, 10f);
            SetRealm(palette);
        }

        public void SetRealm(RealmPalette palette)
        {
            _key.color = Color.Lerp(Color.white, palette.Primary, 0.28f);
            _rim.color = palette.Secondary;
            _portal.color = palette.Primary;
            RenderSettings.ambientLight = Color.Lerp(palette.Sky, palette.Primary, 0.08f);
        }

        private Light Light(string objectName, LightType type, float intensity, float range)
        {
            var item = new GameObject(objectName).AddComponent<Light>();
            item.transform.SetParent(transform, false);
            item.type = type;
            item.intensity = intensity;
            item.range = range;
            return item;
        }
    }
}
