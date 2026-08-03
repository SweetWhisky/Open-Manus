# Unity activation for the RUN//REALMS 2.0 trial build

The repository is ready for either a free Unity Personal license or a Unity Pro trial. Credentials must be created and accepted by the Unity account owner; never commit them to the repository or post them in a PR/chat.

## Recommended: Unity Personal

Unity Personal is free for eligible game developers and can publish Android builds.

1. Create or sign in to your Unity ID.
2. Install Unity Hub on your own computer.
3. In Unity Hub, open **Preferences → Licenses → Add → Get a free personal license**.
4. Locate the generated license file:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
5. In GitHub, open **SweetWhisky/Open-Manus → Settings → Secrets and variables → Actions**.
6. Add these repository secrets:
   - `UNITY_LICENSE`: the complete text contents of `Unity_lic.ulf`
   - `UNITY_EMAIL`: your Unity ID email
   - `UNITY_PASSWORD`: your Unity ID password
7. Open **Actions → RUN REALMS 2 Unity Remaster → Run workflow**, select `run-realms-2-unity-remaster`, and run it.

## Alternative: Unity Pro trial

Start the Pro trial only through your own Unity account because it may convert to a paid subscription unless cancelled.

Add these repository secrets instead:

- `UNITY_SERIAL`: trial serial from Unity
- `UNITY_EMAIL`: Unity ID email
- `UNITY_PASSWORD`: Unity ID password

Then manually run the same GitHub Actions workflow.

## Expected artifact

After tests and compilation pass, GitHub Actions creates:

`RUN-REALMS-2.0-Remaster.apk`

The workflow also creates a SHA-256 checksum. On the feature branch the APK appears under the workflow run's **Artifacts** section. After merge to `main`, the workflow also publishes the GitHub release.

## Security

- Do not paste credentials into chat, source files, commits, issues or pull requests.
- Use repository Actions secrets only.
- Prefer a dedicated Unity account for CI.
- Enable two-factor authentication on the account.
- Rotate the password if it is ever exposed.
