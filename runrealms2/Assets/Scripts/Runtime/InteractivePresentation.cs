using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RunRealms2
{
    public enum RemasterQuality { Performance, Balanced, Ultra }

    public sealed class InteractiveAudio : MonoBehaviour
    {
        private AudioSource _music;
        private AudioSource _ambience;
        private AudioSource _sfx;
        private AudioSource _ui;
        private AudioClip _uiClip;
        private AudioClip _shardClip;
        private AudioClip _hitClip;
        private AudioClip _shieldClip;
        private AudioClip _boostClip;
        private AudioClip _whooshClip;
        private int _realmIndex = -1;

        public void Initialize()
        {
            _music = Source("Music", true, 0.34f);
            _ambience = Source("Ambience", true, 0.22f);
            _sfx = Source("SFX", false, 0.8f);
            _ui = Source("UI", false, 0.55f);
            _uiClip = Tone("ui-click", 0.08f, 740f, 0.22f, 18f, Wave.Square);
            _shardClip = Tone("shard", 0.18f, 1120f, 0.35f, 8f, Wave.Sine, 1.45f);
            _hitClip = Noise("impact", 0.36f, 0.58f, 9f);
            _shieldClip = Tone("shield", 0.45f, 260f, 0.38f, 3.5f, Wave.Triangle, 2.1f);
            _boostClip = Tone("boost", 0.56f, 160f, 0.32f, 2.2f, Wave.Saw, 4.2f);
            _whooshClip = Noise("whoosh", 0.3f, 0.22f, 4.2f, true);
            SetRealm(RuntimeAssets.Palettes[0]);
        }

        public void SetRealm(RealmPalette palette)
        {
            var index = (int)palette.Id;
            if (_realmIndex == index) return;
            _realmIndex = index;
            _music.clip = MusicLoop(palette, index);
            _ambience.clip = AmbientLoop(palette, index);
            _music.Play();
            _ambience.Play();
        }

        public void SetPaused(bool paused)
        {
            _music.pitch = paused ? 0.82f : 1f;
            _ambience.volume = paused ? 0.08f : 0.22f;
        }

        public void SetMasterVolume(float value) => AudioListener.volume = Mathf.Clamp01(value);
        public void PlayUi() => _ui.PlayOneShot(_uiClip);
        public void PlayShard() => _sfx.PlayOneShot(_shardClip, 0.72f);
        public void PlayHit() => _sfx.PlayOneShot(_hitClip, 1f);
        public void PlayNearMiss() => _sfx.PlayOneShot(_whooshClip, 0.68f);

        public void PlayPickup(PickupKind kind)
        {
            _sfx.PlayOneShot(kind switch
            {
                PickupKind.Shield => _shieldClip,
                PickupKind.Boost => _boostClip,
                PickupKind.Magnet => _shardClip,
                _ => _shardClip
            }, 0.9f);
        }

        private AudioSource Source(string label, bool loop, float volume)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.name = label;
            source.loop = loop;
            source.playOnAwake = false;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            return source;
        }

        private enum Wave { Sine, Square, Triangle, Saw }

        private static AudioClip Tone(string name, float duration, float frequency, float volume, float decay, Wave wave, float sweep = 1f)
        {
            const int sampleRate = 44100;
            var samples = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[samples];
            var phase = 0f;
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var progress = i / (float)samples;
                var currentFrequency = frequency * Mathf.Lerp(1f, sweep, progress);
                phase += currentFrequency / sampleRate;
                var oscillator = wave switch
                {
                    Wave.Square => Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2f)),
                    Wave.Triangle => Mathf.PingPong(phase * 4f, 2f) - 1f,
                    Wave.Saw => Mathf.Repeat(phase, 1f) * 2f - 1f,
                    _ => Mathf.Sin(phase * Mathf.PI * 2f)
                };
                data[i] = oscillator * volume * Mathf.Exp(-decay * t) * Mathf.SmoothStep(1f, 0f, progress);
            }
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Noise(string name, float duration, float volume, float decay, bool rising = false)
        {
            const int sampleRate = 44100;
            var samples = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[samples];
            var random = new System.Random(WorldBlueprint.StableHash(name));
            var previous = 0f;
            for (var i = 0; i < samples; i++)
            {
                var progress = i / (float)samples;
                var white = (float)(random.NextDouble() * 2.0 - 1.0);
                previous = Mathf.Lerp(previous, white, rising ? Mathf.Lerp(0.02f, 0.35f, progress) : 0.22f);
                var envelope = rising ? Mathf.Sin(progress * Mathf.PI) : Mathf.Exp(-decay * progress);
                data[i] = previous * volume * envelope;
            }
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MusicLoop(RealmPalette palette, int index)
        {
            const int sampleRate = 44100;
            const float duration = 8f;
            var samples = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[samples * 2];
            var root = 55f * Mathf.Pow(2f, index / 12f);
            var scale = new[] { 1f, 1.1892f, 1.4983f, 2f };
            var random = new System.Random(2400 + index);
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var beat = Mathf.Repeat(t * 2f, 1f);
                var pulse = Mathf.Exp(-beat * 7f);
                var step = Mathf.FloorToInt(t * 2f) % scale.Length;
                var bass = Mathf.Sin(t * root * Mathf.PI * 2f) * 0.12f;
                var arpeggio = Mathf.Sin(t * root * scale[step] * 4f * Mathf.PI * 2f) * 0.055f * pulse;
                var pad = (Mathf.Sin(t * root * 2f * Mathf.PI * 2f) + Mathf.Sin(t * root * 3f * Mathf.PI * 2f) * 0.5f) * 0.035f;
                var tick = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-Mathf.Repeat(t * 4f, 1f) * 22f) * 0.018f;
                var sample = Mathf.Clamp(bass + arpeggio + pad + tick, -0.38f, 0.38f);
                data[i * 2] = sample * (0.92f + Mathf.Sin(t * 0.5f) * 0.08f);
                data[i * 2 + 1] = sample * (0.92f + Mathf.Cos(t * 0.45f) * 0.08f);
            }
            var clip = AudioClip.Create("realm-music-" + palette.Id, samples, 2, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip AmbientLoop(RealmPalette palette, int index)
        {
            const int sampleRate = 22050;
            const float duration = 6f;
            var samples = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[samples];
            var random = new System.Random(8400 + index);
            var filtered = 0f;
            for (var i = 0; i < samples; i++)
            {
                var progress = i / (float)samples;
                var white = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, white, 0.018f + index * 0.002f);
                var drone = Mathf.Sin(progress * Mathf.PI * 2f * (1 + index % 3)) * 0.018f;
                data[i] = filtered * 0.065f + drone;
            }
            var clip = AudioClip.Create("realm-ambience-" + palette.Id, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    public sealed class RemasterHud : MonoBehaviour
    {
        private RemasterGame _game;
        private InteractiveAudio _audio;
        private Canvas _canvas;
        private RectTransform _home;
        private RectTransform _hud;
        private RectTransform _pause;
        private RectTransform _results;
        private RectTransform _settings;
        private Text _score;
        private Text _distance;
        private Text _shards;
        private Text _realm;
        private Text _combo;
        private Text _status;
        private Text _resultText;
        private Image _focusFill;
        private InputField _seedInput;
        private Text _title;
        private float _pulse;

        public void Initialize(RemasterGame game, InteractiveAudio audio)
        {
            _game = game;
            _audio = audio;
            BuildEventSystem();
            BuildCanvas();
            BuildHome();
            BuildHud();
            BuildPause();
            BuildResults();
            BuildSettings();
            ShowHome();
        }

        public void ShowHome()
        {
            SetOnly(_home);
            _status.text = _game.DirectorStatus;
        }

        public void ShowRunning()
        {
            SetOnly(_hud);
        }

        public void ShowPaused()
        {
            _pause.gameObject.SetActive(true);
        }

        public void HidePaused()
        {
            _pause.gameObject.SetActive(false);
            _hud.gameObject.SetActive(true);
        }

        public void ShowResults(int score, float distance, int shards, int nearMisses, string seed)
        {
            SetOnly(_results);
            _resultText.text = $"SCORE {score:N0}\nDISTANCE {distance:N0} m\nSHARDS {shards}\nNEAR MISSES {nearMisses}\nSEED {seed}";
        }

        public void SetDirectorStatus(string status)
        {
            if (_status != null) _status.text = status;
        }

        public void Tick(int score, float distance, int shards, string realm, float focus, float combo)
        {
            if (_score == null) return;
            _score.text = score.ToString("N0");
            _distance.text = $"{distance:N0} m";
            _shards.text = $"◇ {shards}";
            _realm.text = realm;
            _combo.text = $"x{combo:0.0}";
            _focusFill.fillAmount = Mathf.Clamp01(focus / 100f);
            _pulse += Time.unscaledDeltaTime;
            if (_title != null) _title.color = Color.Lerp(RuntimeAssets.Hex("23D7FF"), RuntimeAssets.Hex("FF3BD4"), Mathf.PingPong(_pulse * 0.35f, 1f));
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("RemasterUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildHome()
        {
            _home = Panel("Home", new Color(0.015f, 0.027f, 0.05f, 0.92f));
            _title = Label(_home, "RUN//REALMS", 88, TextAnchor.MiddleCenter, RuntimeAssets.Hex("23D7FF"));
            Anchor(_title.rectTransform, 0.1f, 0.72f, 0.9f, 0.94f);
            var subtitle = Label(_home, "2.0 UNITY REMASTER  •  EVERY RUN BUILDS A NEW 3D WORLD", 22, TextAnchor.MiddleCenter, RuntimeAssets.Hex("9BA8B7"));
            Anchor(subtitle.rectTransform, 0.12f, 0.65f, 0.88f, 0.75f);

            _seedInput = Input(_home, "RUN-REALMS-2");
            Anchor(_seedInput.GetComponent<RectTransform>(), 0.3f, 0.52f, 0.7f, 0.61f);
            var start = Button(_home, "START REMASTER RUN", RuntimeAssets.Hex("23D7FF"), () => _game.RequestRun(_seedInput.text));
            Anchor(start.GetComponent<RectTransform>(), 0.3f, 0.39f, 0.7f, 0.49f);
            var daily = Button(_home, "DAILY WORLD", RuntimeAssets.Hex("FF3BD4"), _game.RequestDailyRun);
            Anchor(daily.GetComponent<RectTransform>(), 0.3f, 0.27f, 0.49f, 0.36f);
            var random = Button(_home, "RANDOM SEED", RuntimeAssets.Hex("8B5CFF"), () => _seedInput.text = "R2-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());
            Anchor(random.GetComponent<RectTransform>(), 0.51f, 0.27f, 0.7f, 0.36f);
            var settings = Button(_home, "GRAPHICS / AUDIO", RuntimeAssets.Hex("0B2035"), () => SetOnly(_settings));
            Anchor(settings.GetComponent<RectTransform>(), 0.3f, 0.15f, 0.7f, 0.24f);
            _status = Label(_home, "OFFLINE DIRECTOR", 16, TextAnchor.MiddleCenter, RuntimeAssets.Hex("67FF80"));
            Anchor(_status.rectTransform, 0.2f, 0.06f, 0.8f, 0.12f);
        }

        private void BuildHud()
        {
            _hud = Panel("HUD", Color.clear);
            _score = Label(_hud, "0", 42, TextAnchor.UpperRight, Color.white);
            Anchor(_score.rectTransform, 0.72f, 0.88f, 0.97f, 0.98f);
            _distance = Label(_hud, "0 m", 36, TextAnchor.UpperLeft, Color.white);
            Anchor(_distance.rectTransform, 0.03f, 0.88f, 0.28f, 0.98f);
            _realm = Label(_hud, "NEON RAIN CITY", 20, TextAnchor.UpperCenter, RuntimeAssets.Hex("23D7FF"));
            Anchor(_realm.rectTransform, 0.33f, 0.9f, 0.67f, 0.98f);
            _shards = Label(_hud, "◇ 0", 25, TextAnchor.UpperRight, RuntimeAssets.Hex("FFB547"));
            Anchor(_shards.rectTransform, 0.77f, 0.81f, 0.97f, 0.9f);
            _combo = Label(_hud, "x1.0", 28, TextAnchor.MiddleCenter, RuntimeAssets.Hex("FF3BD4"));
            Anchor(_combo.rectTransform, 0.43f, 0.79f, 0.57f, 0.88f);

            var focusBackground = Image(_hud, new Color(0f, 0f, 0f, 0.58f));
            Anchor(focusBackground.rectTransform, 0.37f, 0.94f, 0.63f, 0.965f);
            _focusFill = Image(focusBackground.rectTransform, RuntimeAssets.Hex("23D7FF"));
            Anchor(_focusFill.rectTransform, 0f, 0f, 1f, 1f);
            _focusFill.type = Image.Type.Filled;
            _focusFill.fillMethod = Image.FillMethod.Horizontal;
            _focusFill.fillAmount = 1f;

            var pauseButton = Button(_hud, "Ⅱ", new Color(0.03f, 0.08f, 0.13f, 0.82f), _game.Pause);
            Anchor(pauseButton.GetComponent<RectTransform>(), 0.92f, 0.69f, 0.98f, 0.79f);
            var controls = Label(_hud, "SWIPE  ◀ ▶   •   UP JUMP   •   DOWN SLIDE   •   TAP FOCUS", 16, TextAnchor.LowerCenter, new Color(0.85f, 0.92f, 1f, 0.75f));
            Anchor(controls.rectTransform, 0.2f, 0.02f, 0.8f, 0.09f);
        }

        private void BuildPause()
        {
            _pause = Panel("Pause", new Color(0.01f, 0.02f, 0.04f, 0.86f));
            var label = Label(_pause, "REALM PAUSED", 58, TextAnchor.MiddleCenter, Color.white);
            Anchor(label.rectTransform, 0.25f, 0.65f, 0.75f, 0.82f);
            var resume = Button(_pause, "RESUME", RuntimeAssets.Hex("23D7FF"), _game.Resume);
            Anchor(resume.GetComponent<RectTransform>(), 0.36f, 0.49f, 0.64f, 0.59f);
            var restart = Button(_pause, "RESTART SEED", RuntimeAssets.Hex("8B5CFF"), _game.Restart);
            Anchor(restart.GetComponent<RectTransform>(), 0.36f, 0.36f, 0.64f, 0.46f);
            var home = Button(_pause, "QUIT TO HUB", RuntimeAssets.Hex("6B1B31"), _game.QuitToHome);
            Anchor(home.GetComponent<RectTransform>(), 0.36f, 0.23f, 0.64f, 0.33f);
        }

        private void BuildResults()
        {
            _results = Panel("Results", new Color(0.015f, 0.027f, 0.05f, 0.94f));
            var heading = Label(_results, "RUN COMPLETE", 60, TextAnchor.MiddleCenter, RuntimeAssets.Hex("23D7FF"));
            Anchor(heading.rectTransform, 0.22f, 0.73f, 0.78f, 0.88f);
            _resultText = Label(_results, "", 28, TextAnchor.MiddleCenter, Color.white);
            Anchor(_resultText.rectTransform, 0.25f, 0.35f, 0.75f, 0.72f);
            var replay = Button(_results, "REPLAY SAME SEED", RuntimeAssets.Hex("23D7FF"), _game.Restart);
            Anchor(replay.GetComponent<RectTransform>(), 0.25f, 0.2f, 0.49f, 0.3f);
            var hub = Button(_results, "RETURN TO HUB", RuntimeAssets.Hex("8B5CFF"), _game.QuitToHome);
            Anchor(hub.GetComponent<RectTransform>(), 0.51f, 0.2f, 0.75f, 0.3f);
        }

        private void BuildSettings()
        {
            _settings = Panel("Settings", new Color(0.015f, 0.027f, 0.05f, 0.96f));
            var heading = Label(_settings, "REMASTER SETTINGS", 54, TextAnchor.MiddleCenter, RuntimeAssets.Hex("23D7FF"));
            Anchor(heading.rectTransform, 0.2f, 0.76f, 0.8f, 0.9f);
            var performance = Button(_settings, "PERFORMANCE  •  60 FPS", RuntimeAssets.Hex("0E5A68"), () => _game.SetQuality(RemasterQuality.Performance));
            Anchor(performance.GetComponent<RectTransform>(), 0.27f, 0.58f, 0.73f, 0.68f);
            var balanced = Button(_settings, "BALANCED  •  90 FPS", RuntimeAssets.Hex("29438A"), () => _game.SetQuality(RemasterQuality.Balanced));
            Anchor(balanced.GetComponent<RectTransform>(), 0.27f, 0.45f, 0.73f, 0.55f);
            var ultra = Button(_settings, "ULTRA  •  120 FPS", RuntimeAssets.Hex("71306F"), () => _game.SetQuality(RemasterQuality.Ultra));
            Anchor(ultra.GetComponent<RectTransform>(), 0.27f, 0.32f, 0.73f, 0.42f);
            var mute = Button(_settings, "TOGGLE AUDIO", RuntimeAssets.Hex("3B2F18"), _game.ToggleAudio);
            Anchor(mute.GetComponent<RectTransform>(), 0.27f, 0.19f, 0.49f, 0.29f);
            var close = Button(_settings, "BACK", RuntimeAssets.Hex("0B2035"), ShowHome);
            Anchor(close.GetComponent<RectTransform>(), 0.51f, 0.19f, 0.73f, 0.29f);
        }

        private RectTransform Panel(string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(_canvas.transform, false);
            Anchor(panel, 0f, 0f, 1f, 1f);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private Text Label(Transform parent, string value, int size, TextAnchor anchor, Color color)
        {
            var label = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = anchor;
            label.color = color;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, size / 2);
            label.resizeTextMaxSize = size;
            return label;
        }

        private Button Button(Transform parent, string value, Color color, UnityEngine.Events.UnityAction action)
        {
            var root = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.color = color;
            var button = root.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.22f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() => _audio.PlayUi());
            button.onClick.AddListener(action);
            var text = Label(root.transform, value, 22, TextAnchor.MiddleCenter, Color.white);
            Anchor(text.rectTransform, 0.03f, 0.04f, 0.97f, 0.96f);
            return button;
        }

        private InputField Input(Transform parent, string value)
        {
            var root = new GameObject("SeedInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(0.03f, 0.08f, 0.13f, 0.95f);
            var input = root.GetComponent<InputField>();
            var text = Label(root.transform, value, 24, TextAnchor.MiddleCenter, Color.white);
            Anchor(text.rectTransform, 0.04f, 0.08f, 0.96f, 0.92f);
            input.textComponent = text;
            input.text = value;
            input.characterLimit = 32;
            input.contentType = InputField.ContentType.Alphanumeric;
            return input;
        }

        private Image Image(Transform parent, Color color)
        {
            var image = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            return image;
        }

        private void SetOnly(RectTransform visible)
        {
            foreach (var panel in new[] { _home, _hud, _pause, _results, _settings }) if (panel != null) panel.gameObject.SetActive(panel == visible);
        }

        private static void BuildEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void Anchor(RectTransform target, float minX, float minY, float maxX, float maxY)
        {
            target.anchorMin = new Vector2(minX, minY);
            target.anchorMax = new Vector2(maxX, maxY);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }
    }
}
