# Self-hosted AI stack

This branch adds a legitimate self-hosted path for OpenManus so usage is constrained by your own hardware/model context rather than a third-party per-request token quota.

## Text / agent stack

### Ollama
Repository: https://github.com/ollama/ollama

Install Ollama, then pull/run Qwen:

```bash
ollama run qwen3
```

Copy the included config example:

```bash
cp config/config.ollama-qwen.example.toml config/config.toml
python main.py
```

### vLLM
Repository: https://github.com/vllm-project/vllm

Use vLLM when you have a suitable NVIDIA GPU/server and want higher-throughput OpenAI-compatible serving for supported open models.

### llama.cpp
Repository: https://github.com/ggml-org/llama.cpp

Use llama.cpp for local GGUF models, especially CPU/Apple Silicon or quantized deployments.

## Image / video / 3D stack

### ComfyUI
Repository: https://github.com/Comfy-Org/ComfyUI

ComfyUI can run image, video, audio, 3D, and text workflows locally. For a provider-independent workflow, keep paid API nodes disabled and use locally downloaded model weights that permit your intended use.

Useful open video projects:

- Wan 2.1: https://github.com/Wan-Video/Wan2.1
- LTX-Video: https://github.com/Lightricks/LTX-Video
- HunyuanVideo: https://github.com/Tencent-Hunyuan/HunyuanVideo

These are alternatives to hosted generation services; they do not unlock or bypass any hosted provider's account limits.

## GPT-6 Astra

GPT-6 Astra is a hosted OpenAI model. A repository cannot grant access, extra paid tokens, or higher rate limits. Use the official OpenAI API with an authorized project and billing/usage tier if you want `gpt-6-astra`.

Do not commit API keys to GitHub. Keep provider credentials in environment variables or secret stores.
