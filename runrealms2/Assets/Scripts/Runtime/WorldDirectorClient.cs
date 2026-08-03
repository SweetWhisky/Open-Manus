using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RunRealms2
{
    [Serializable]
    public sealed class RemasterConfig
    {
        public string worldDirectorEndpoint = "";
        public string addressablesCatalogUrl = "";
        public int requestTimeoutSeconds = 3;
        public bool allowOnlineBlueprints = true;
        public string buildChannel = "remaster-preview";
    }

    [Serializable]
    internal sealed class BlueprintRequest
    {
        public string seed;
        public float skill;
        public string clientBuild;
        public string platform;
    }

    public sealed class WorldDirectorClient : MonoBehaviour
    {
        public RemasterConfig Config { get; private set; } = new();
        public bool ConfigLoaded { get; private set; }
        public string Status { get; private set; } = "OFFLINE DIRECTOR";

        public IEnumerator Initialize()
        {
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, "remaster-config.json");
            using var request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var parsed = JsonUtility.FromJson<RemasterConfig>(request.downloadHandler.text);
                    if (parsed != null) Config = parsed;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"RUN//REALMS config parse failed: {exception.Message}");
                }
            }
            ConfigLoaded = true;
            Status = CanUseNetwork ? "SERVER BLUEPRINTS READY" : "OFFLINE DIRECTOR";
        }

        public IEnumerator GetBlueprint(string seed, float playerSkill, Action<WorldBlueprint, bool> complete)
        {
            var fallback = WorldBlueprint.Local(seed);
            if (!CanUseNetwork)
            {
                complete?.Invoke(fallback, false);
                yield break;
            }

            var payload = new BlueprintRequest
            {
                seed = seed,
                skill = Mathf.Clamp01(playerSkill),
                clientBuild = Application.version,
                platform = Application.platform.ToString()
            };
            var body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using var request = new UnityWebRequest(Config.worldDirectorEndpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.Clamp(Config.requestTimeoutSeconds, 1, 8)
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Status = "OFFLINE FALLBACK";
                complete?.Invoke(fallback, false);
                yield break;
            }

            try
            {
                var blueprint = JsonUtility.FromJson<WorldBlueprint>(request.downloadHandler.text);
                if (blueprint != null && blueprint.IsValid())
                {
                    blueprint.seed = seed;
                    Status = "SERVER BLUEPRINT ACTIVE";
                    complete?.Invoke(blueprint, true);
                    yield break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"RUN//REALMS blueprint rejected: {exception.Message}");
            }

            Status = "INVALID BLUEPRINT — OFFLINE FALLBACK";
            complete?.Invoke(fallback, false);
        }

        private bool CanUseNetwork => ConfigLoaded
                                      && Config.allowOnlineBlueprints
                                      && !string.IsNullOrWhiteSpace(Config.worldDirectorEndpoint)
                                      && Config.worldDirectorEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
