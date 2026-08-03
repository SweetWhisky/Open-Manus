using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RunRealms2
{
    [Serializable]
    public sealed class WorldBlueprint
    {
        public string seed;
        public int[] realmOrder = { 0, 1, 2, 3, 4, 5, 6, 7 };
        public float[] intensityCurve = { 0.2f, 0.32f, 0.46f, 0.62f, 0.78f, 0.92f };
        public string theme = "Offline deterministic remaster";

        public static WorldBlueprint Local(string runSeed)
        {
            var order = Enumerable.Range(0, RuntimeAssets.Palettes.Length).ToArray();
            var random = new System.Random(StableHash(runSeed + "|REALMS"));
            for (var i = order.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return new WorldBlueprint { seed = runSeed, realmOrder = order };
        }

        public bool IsValid()
        {
            if (realmOrder == null || realmOrder.Length != RuntimeAssets.Palettes.Length) return false;
            if (realmOrder.Distinct().Count() != RuntimeAssets.Palettes.Length) return false;
            if (realmOrder.Any(value => value < 0 || value >= RuntimeAssets.Palettes.Length)) return false;
            if (intensityCurve == null || intensityCurve.Length < 4) return false;
            if (intensityCurve.Any(value => value < 0.1f || value > 1f)) return false;
            return true;
        }

        public static int StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }

    public sealed class ProceduralWorld : MonoBehaviour
    {
        public event Action<RealmPalette> RealmChanged;

        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public RealmPalette CurrentPalette { get; private set; }
        public string BlueprintTheme => _blueprint?.theme ?? "Offline deterministic remaster";

        private const float ChunkLength = 30f;
        private const int ActiveChunkCount = 14;
        private readonly List<WorldChunk> _chunks = new();
        private RunnerMotor _runner;
        private WorldBlueprint _blueprint;
        private System.Random _random;
        private int _chunkSerial;
        private int _realmStage;
        private bool _running;
        private ParticleSystem _weather;
        private Transform _worldRoot;

        public void Initialize(RunnerMotor runner)
        {
            _runner = runner;
            _worldRoot = new GameObject("StreamedWorld").transform;
            _worldRoot.SetParent(transform, false);
            _weather = BuildWeatherSystem();
        }

        public void BeginRun(string seed, WorldBlueprint blueprint)
        {
            _blueprint = blueprint != null && blueprint.IsValid() ? blueprint : WorldBlueprint.Local(seed);
            _random = new System.Random(WorldBlueprint.StableHash(seed + "|WORLD"));
            _chunkSerial = 0;
            _realmStage = 0;
            Distance = 0f;
            Speed = 14f;
            CurrentPalette = PaletteForStage(0);
            ApplyRealm(CurrentPalette);

            while (_chunks.Count < ActiveChunkCount)
            {
                var root = new GameObject("WorldChunk").transform;
                root.SetParent(_worldRoot, false);
                _chunks.Add(new WorldChunk(root));
            }

            for (var i = 0; i < _chunks.Count; i++)
            {
                _chunks[i].Root.localPosition = new Vector3(0f, 0f, i * ChunkLength - 15f);
                RebuildChunk(_chunks[i]);
            }
            _running = true;
        }

        public void SetRunning(bool running) => _running = running;

        public void Tick(float deltaTime)
        {
            if (!_running || _runner == null) return;
            Speed = Mathf.Min(32f, 14f + Distance / 260f);
            var travel = Speed * _runner.WorldSpeedScale * deltaTime;
            Distance += travel;

            var furthest = float.MinValue;
            foreach (var chunk in _chunks)
            {
                chunk.Root.localPosition += Vector3.back * travel;
                furthest = Mathf.Max(furthest, chunk.Root.localPosition.z);
            }

            foreach (var chunk in _chunks)
            {
                if (chunk.Root.localPosition.z < -ChunkLength * 1.25f)
                {
                    chunk.Root.localPosition = new Vector3(0f, 0f, furthest + ChunkLength);
                    furthest = chunk.Root.localPosition.z;
                    RebuildChunk(chunk);
                }
            }

            if (_runner.MagnetActive) AttractShards(deltaTime);

            var stage = Mathf.FloorToInt(Distance / 620f);
            if (stage != _realmStage)
            {
                _realmStage = stage;
                CurrentPalette = PaletteForStage(stage);
                ApplyRealm(CurrentPalette);
                _runner.SetPalette(CurrentPalette);
                RealmChanged?.Invoke(CurrentPalette);
            }
        }

        private RealmPalette PaletteForStage(int stage)
        {
            var order = _blueprint?.realmOrder;
            var index = order != null && order.Length > 0 ? order[Mathf.Abs(stage) % order.Length] : Mathf.Abs(stage) % RuntimeAssets.Palettes.Length;
            return RuntimeAssets.Palettes[index];
        }

        private void RebuildChunk(WorldChunk chunk)
        {
            chunk.Clear();
            var palette = PaletteForStage(Mathf.FloorToInt((_chunkSerial * ChunkLength) / 620f));
            var localSeed = _random.Next();
            var random = new System.Random(localSeed);
            BuildRoad(chunk, palette, random);
            BuildCity(chunk, palette, random);
            BuildGameplay(chunk, palette, random, _chunkSerial);
            if (_chunkSerial % 3 == 0) BuildCrowd(chunk, palette, random);
            _chunkSerial++;
        }

        private void BuildRoad(WorldChunk chunk, RealmPalette palette, System.Random random)
        {
            var road = chunk.Primitive(PrimitiveType.Cube, "Road");
            road.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            road.transform.localScale = new Vector3(9.6f, 0.55f, ChunkLength);
            road.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material("road-" + palette.Id, palette.Ground, 0.2f, 0.58f);
            DisableCollider(road);

            for (var lane = -1; lane <= 1; lane += 2)
            {
                var stripe = chunk.Primitive(PrimitiveType.Cube, "LaneGlow");
                stripe.transform.localPosition = new Vector3(lane * 1.33f, 0.015f, 0f);
                stripe.transform.localScale = new Vector3(0.055f, 0.025f, ChunkLength * 0.98f);
                stripe.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material("lane-" + palette.Id, palette.Primary, 0f, 0.9f, true);
                DisableCollider(stripe);
            }

            var edgeMaterial = RuntimeAssets.Material("edge-" + palette.Id, palette.Secondary, 0.1f, 0.85f, true);
            foreach (var side in new[] { -1f, 1f })
            {
                var edge = chunk.Primitive(PrimitiveType.Cube, "EdgeLight");
                edge.transform.localPosition = new Vector3(side * 4.72f, 0.14f, 0f);
                edge.transform.localScale = new Vector3(0.12f, 0.16f, ChunkLength);
                edge.GetComponent<Renderer>().sharedMaterial = edgeMaterial;
                DisableCollider(edge);
            }
        }

        private void BuildCity(WorldChunk chunk, RealmPalette palette, System.Random random)
        {
            for (var sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                var side = sideIndex == 0 ? -1f : 1f;
                for (var row = 0; row < 4; row++)
                {
                    var height = Mathf.Lerp(5f, 18f, (float)random.NextDouble());
                    var width = Mathf.Lerp(2.6f, 5.5f, (float)random.NextDouble());
                    var depth = Mathf.Lerp(3.5f, 7f, (float)random.NextDouble());
                    var building = chunk.Primitive(PrimitiveType.Cube, "Building");
                    building.transform.localPosition = new Vector3(side * (7.1f + row * 2.8f), height * 0.5f - 0.1f, -10.5f + row * 7f + (float)random.NextDouble() * 2f);
                    building.transform.localScale = new Vector3(width, height, depth);
                    building.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.WindowMaterial(palette, random.Next());
                    DisableCollider(building);

                    if (random.NextDouble() > 0.45)
                    {
                        var sign = chunk.Primitive(PrimitiveType.Cube, "HolographicSign");
                        sign.transform.localPosition = building.transform.localPosition + new Vector3(-side * (width * 0.51f), height * 0.12f, 0f);
                        sign.transform.localScale = new Vector3(0.08f, Mathf.Lerp(0.7f, 1.8f, (float)random.NextDouble()), Mathf.Lerp(1f, 2.6f, (float)random.NextDouble()));
                        sign.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material("sign-" + palette.Id + random.Next(3), random.NextDouble() > 0.5 ? palette.Primary : palette.Secondary, 0f, 0.92f, true);
                        DisableCollider(sign);
                    }
                }
            }

            if (palette.Id is RealmId.OvergrownLab or RealmId.CelestialRuins)
            {
                for (var i = 0; i < 5; i++)
                {
                    var column = chunk.Primitive(PrimitiveType.Cylinder, "AncientColumn");
                    column.transform.localPosition = new Vector3((random.NextDouble() > 0.5 ? -1f : 1f) * Mathf.Lerp(5.6f, 9f, (float)random.NextDouble()), 1.8f, Mathf.Lerp(-14f, 14f, (float)random.NextDouble()));
                    column.transform.localScale = new Vector3(0.45f, Mathf.Lerp(1.6f, 3f, (float)random.NextDouble()), 0.45f);
                    column.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material("column-" + palette.Id, Color.Lerp(palette.Ground, palette.Primary, 0.25f), 0.15f, 0.38f);
                    DisableCollider(column);
                }
            }
        }

        private void BuildGameplay(WorldChunk chunk, RealmPalette palette, System.Random random, int serial)
        {
            if (serial < 2)
            {
                BuildShardLine(chunk, palette, 1, -7f, 5);
                return;
            }

            var difficulty = Mathf.Clamp01(0.18f + Distance / 3500f);
            var safeLane = random.Next(0, 3);
            var pattern = random.Next(0, difficulty > 0.62f ? 7 : 5);
            var z = -4f;

            switch (pattern)
            {
                case 0:
                    BuildHazard(chunk, palette, random.Next(0, 3), z, random.NextDouble() > 0.45 ? AvoidanceKind.Jump : AvoidanceKind.Slide);
                    break;
                case 1:
                    for (var lane = 0; lane < 3; lane++) if (lane != safeLane) BuildHazard(chunk, palette, lane, z, AvoidanceKind.Jump);
                    BuildShardLine(chunk, palette, safeLane, z + 2f, 4);
                    break;
                case 2:
                    BuildHazard(chunk, palette, safeLane, z, AvoidanceKind.Jump);
                    BuildShardLine(chunk, palette, safeLane, z + 1.2f, 4, 0.55f);
                    break;
                case 3:
                    BuildHazard(chunk, palette, safeLane, z, AvoidanceKind.Slide);
                    BuildShardLine(chunk, palette, (safeLane + 1) % 3, z + 2f, 5);
                    break;
                case 4:
                    BuildHazard(chunk, palette, 0, z, AvoidanceKind.Jump);
                    BuildHazard(chunk, palette, 2, z + 5f, AvoidanceKind.Slide);
                    BuildShardLine(chunk, palette, 1, z + 1.2f, 6);
                    break;
                case 5:
                    BuildHazard(chunk, palette, (safeLane + 1) % 3, z, AvoidanceKind.Jump);
                    BuildHazard(chunk, palette, (safeLane + 2) % 3, z + 4.5f, AvoidanceKind.Slide);
                    BuildPowerup(chunk, palette, safeLane, z + 2.4f, (PickupKind)random.Next(1, 4));
                    break;
                default:
                    for (var step = 0; step < 3; step++)
                    {
                        var blocked = (safeLane + step + 1) % 3;
                        BuildHazard(chunk, palette, blocked, z + step * 4.2f, step % 2 == 0 ? AvoidanceKind.Jump : AvoidanceKind.Slide);
                        BuildPickup(chunk, palette, PickupKind.Shard, safeLane, z + step * 4.2f + 1.2f, 1);
                    }
                    break;
            }

            if (random.NextDouble() < 0.18) BuildPowerup(chunk, palette, random.Next(0, 3), 9f, (PickupKind)random.Next(1, 4));
        }

        private void BuildHazard(WorldChunk chunk, RealmPalette palette, int lane, float z, AvoidanceKind avoidance)
        {
            var primitive = avoidance == AvoidanceKind.Slide ? PrimitiveType.Cube : PrimitiveType.Cube;
            var hazard = chunk.Primitive(primitive, avoidance == AvoidanceKind.Slide ? "OverheadBarrier" : "JumpBarrier");
            hazard.transform.localPosition = new Vector3((lane - 1) * 2.65f, avoidance == AvoidanceKind.Slide ? 1.75f : 0.62f, z);
            hazard.transform.localScale = avoidance == AvoidanceKind.Slide ? new Vector3(1.9f, 0.42f, 0.65f) : new Vector3(1.55f, 1.25f, 0.72f);
            hazard.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material("hazard-" + palette.Id, palette.Secondary, 0.25f, 0.78f, true);
            var collider = hazard.GetComponent<Collider>();
            collider.isTrigger = true;
            var interactable = hazard.AddComponent<WorldInteractable>();
            interactable.IsHazard = true;
            interactable.Avoidance = avoidance;
        }

        private void BuildShardLine(WorldChunk chunk, RealmPalette palette, int lane, float startZ, int count, float height = 0.8f)
        {
            for (var i = 0; i < count; i++) BuildPickup(chunk, palette, PickupKind.Shard, lane, startZ + i * 1.8f, 1, height + Mathf.Sin(i * 0.8f) * 0.24f);
        }

        private void BuildPowerup(WorldChunk chunk, RealmPalette palette, int lane, float z, PickupKind kind)
        {
            BuildPickup(chunk, palette, kind, lane, z, 1, 0.95f);
        }

        private void BuildPickup(WorldChunk chunk, RealmPalette palette, PickupKind kind, int lane, float z, int value, float height = 0.8f)
        {
            var pickup = chunk.Primitive(kind == PickupKind.Shard ? PrimitiveType.Sphere : PrimitiveType.Cylinder, kind.ToString());
            pickup.transform.localPosition = new Vector3((lane - 1) * 2.65f, height, z);
            pickup.transform.localScale = kind == PickupKind.Shard ? Vector3.one * 0.28f : new Vector3(0.36f, 0.18f, 0.36f);
            var color = kind switch
            {
                PickupKind.Shield => RuntimeAssets.Hex("67FF80"),
                PickupKind.Magnet => RuntimeAssets.Hex("FF3BD4"),
                PickupKind.Boost => RuntimeAssets.Hex("FFB547"),
                _ => Color.Lerp(Color.white, palette.Primary, 0.35f)
            };
            pickup.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material("pickup-" + kind, color, 0f, 0.92f, true);
            var collider = pickup.GetComponent<Collider>();
            collider.isTrigger = true;
            var interactable = pickup.AddComponent<WorldInteractable>();
            interactable.Pickup = kind;
            interactable.Value = value;
            pickup.AddComponent<PickupSpinner>().Speed = kind == PickupKind.Shard ? 145f : 95f;
        }

        private void BuildCrowd(WorldChunk chunk, RealmPalette palette, System.Random random)
        {
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var actor = new GameObject("RealmCitizen");
                actor.transform.SetParent(chunk.Root, false);
                actor.transform.localPosition = new Vector3(side * 5.75f, 0f, Mathf.Lerp(-9f, 9f, (float)random.NextDouble()));
                actor.transform.localRotation = Quaternion.Euler(0f, side < 0f ? 65f : -65f, 0f);
                var humanoid = ProceduralCharacterFactory.CreateHumanoid(actor.transform, palette, false);
                actor.AddComponent<CrowdActor>().Initialize(humanoid, random.NextDouble() > 0.45);
                chunk.RegisterOwned(actor);
            }
        }

        private ParticleSystem BuildWeatherSystem()
        {
            var objectRoot = new GameObject("RealmWeather");
            objectRoot.transform.SetParent(transform, false);
            objectRoot.transform.localPosition = new Vector3(0f, 9f, 8f);
            var particles = objectRoot.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = 2.2f;
            main.startSpeed = 9f;
            main.startSize = 0.08f;
            main.maxParticles = 420;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 85f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 1f, 32f);
            objectRoot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            particles.Play();
            return particles;
        }

        private void ApplyRealm(RealmPalette palette)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0075f;
            RenderSettings.fogColor = palette.Fog;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(palette.Sky, Color.white, 0.08f);
            RenderSettings.ambientEquatorColor = palette.Ground;
            RenderSettings.ambientGroundColor = palette.Sky * 0.32f;
            Camera.main.backgroundColor = palette.Sky;

            var particleMain = _weather.main;
            particleMain.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(palette.Primary, Color.white, 0.35f), palette.Secondary);
            particleMain.startSize = palette.Id == RealmId.NeonRainCity ? new ParticleSystem.MinMaxCurve(0.035f, 0.09f) : new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        }

        private void AttractShards(float deltaTime)
        {
            foreach (var chunk in _chunks)
            {
                foreach (var interactable in chunk.Root.GetComponentsInChildren<WorldInteractable>(false))
                {
                    if (interactable.Consumed || interactable.IsHazard || interactable.Pickup != PickupKind.Shard) continue;
                    var distance = Vector3.Distance(interactable.transform.position, _runner.transform.position);
                    if (distance < 9f) interactable.transform.position = Vector3.MoveTowards(interactable.transform.position, _runner.transform.position + Vector3.up, 15f * deltaTime);
                }
            }
        }

        private static void DisableCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }
    }

    public sealed class WorldChunk
    {
        public Transform Root { get; }
        private readonly List<GameObject> _pooled = new();
        private readonly List<GameObject> _owned = new();

        public WorldChunk(Transform root) => Root = root;

        public GameObject Primitive(PrimitiveType type, string name)
        {
            var item = RuntimeAssets.GetPrimitive(type, Root, name);
            var oldInteractable = item.GetComponent<WorldInteractable>();
            if (oldInteractable != null) UnityEngine.Object.Destroy(oldInteractable);
            var spinner = item.GetComponent<PickupSpinner>();
            if (spinner != null) UnityEngine.Object.Destroy(spinner);
            var collider = item.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }
            _pooled.Add(item);
            return item;
        }

        public void RegisterOwned(GameObject item) => _owned.Add(item);

        public void Clear()
        {
            foreach (var item in _pooled) RuntimeAssets.Release(item);
            _pooled.Clear();
            foreach (var item in _owned) if (item != null) UnityEngine.Object.Destroy(item);
            _owned.Clear();
        }
    }

    public sealed class PickupSpinner : MonoBehaviour
    {
        public float Speed = 120f;
        private float _baseY;

        private void OnEnable() => _baseY = transform.localPosition.y;

        private void Update()
        {
            transform.Rotate(0f, Speed * Time.deltaTime, Speed * 0.25f * Time.deltaTime, Space.Self);
            var position = transform.localPosition;
            position.y = _baseY + Mathf.Sin(Time.time * 3f + transform.localPosition.z) * 0.08f;
            transform.localPosition = position;
        }
    }

    public sealed class CrowdActor : MonoBehaviour
    {
        private ProceduralHumanoid _humanoid;
        private bool _dance;
        private float _phase;

        public void Initialize(ProceduralHumanoid humanoid, bool dance)
        {
            _humanoid = humanoid;
            _dance = dance;
            _phase = UnityEngine.Random.value * 10f;
            transform.localScale = Vector3.one * UnityEngine.Random.Range(0.78f, 0.96f);
        }

        private void Update()
        {
            if (_humanoid == null) return;
            var time = Time.time + _phase;
            _humanoid.Tick(time * (_dance ? 1.35f : 0.42f), 0f, 0f, false, false, _dance);
            transform.localPosition += Vector3.up * (Mathf.Sin(time * 2.4f) * 0.0015f);
        }
    }
}
