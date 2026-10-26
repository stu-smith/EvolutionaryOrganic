# itch.io

Builds `out\payload\` — the folder butler uploads — and `out\Morphogen-itch-win-x64.zip`, the same
thing packed up for the web form if butler is not to hand.

```
pwsh -File installer/itch/build.ps1          # build only
pwsh -File installer/itch/build.ps1 -Push    # build and publish to whitelightrising/morphogen:windows
```

`-SkipPublish` reuses `obj/publish`. `-FetchButler` downloads butler into `obj/butler` instead of
expecting it on PATH. `-Target user/game` (or `ITCH_TARGET`) pushes somewhere other than
`whitelightrising/morphogen`.

## Setting it up

1. The page must already exist on itch.io, with *Kind of project* set to **Downloadable**. butler
   cannot create it.
2. Run `butler login` once, or put an API key from <https://itch.io/user/settings/api-keys> in
   `BUTLER_API_KEY`. The script refuses to push with neither.

## The version comes from the tags

With no `-Version`, the build is stamped with the most recent `vX.Y.Z` git tag. With no such tag it
stamps `0.0.0` and says so in yellow: obviously wrong on the storefront, rather than quietly wrong.

## Self-contained, on purpose

An itch.io download is expected to run out of the folder it arrives in, and neither the itch app
nor a plain unzip will install the .NET Desktop Runtime for you. The runtime comes along, which is
most of the payload size.

## butler uploads a folder, not an archive

`butler push` diffs the payload folder against what is already on the channel and uploads only what
changed. Pushing the zip would re-upload everything every time and leave users with a zip inside
their download. `out\payload\` is rebuilt from scratch on every run so a stale file is never
uploaded.

## The manifest

`itch.toml` is copied into the payload as `.itch.toml`. It tells the itch app that Launch runs
`Morphogen.exe` and adds a menu entry for `readme.txt`. Anyone who unzips by hand never sees it,
which is why `readme.txt` says the same things.

## Channels

The default channel is `windows`; butler reads the platform from the channel name. It is created on
first push.

## Publishing is deliberate

`-Push` is opt-in. itch.io has no draft state for a build: once butler finishes, it is live and the
itch app offers it as an update.
