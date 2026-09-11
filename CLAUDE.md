# CLAUDE.md

このリポジトリで作業するエージェント向けの指針。

## プロジェクトの目的

Spotify Web API から曲・アルバム・アーティストのメタデータとアートワークを取得して
Jellyfin に保存するプラグイン。

姉妹プラグイン [Jellyfin-AppleMusic-Metadata][apple] と設計・CI・リリース経路を共有
する。キャッシュ・スロットル・プロバイダの分離で迷ったら向こうを見る。ただし API の
意味は Spotify 公式を優先し、Apple の storefront モデルを持ち込まない。

利用者向けの説明は [README.md](README.md)、開発手順は
[docs/development.md](docs/development.md)。

## 決定済みの設計（勝手に変えない）

1. **取得元は Spotify Web API。** 認証は Client Credentials flow。
   `SpotifyTokenProvider` がトークンを 1 分早く更新し、`SpotifyTransport` が bearer
   token を付ける。401 は token を捨てて 1 回だけ再試行、403 は認証エラー、429 は
   `Retry-After` を `CatalogRateLimitedException` に載せる。
2. **資格情報を配布物・リポジトリに入れない。** Client ID / secret は利用者が
   Spotify Developer Dashboard で作り、Jellyfin の設定画面に入れる。
3. **Spotify ID は世界共通。** Market（既定 `JP`）は availability と track relinking
   に使うだけで、provider id と一緒に保存しない。Apple の storefront フォールバックを
   コピーしない。
4. **カタログ取得元は差し替え可能にする。** transport は生 JSON を返す。
   DTO・デシリアライズ・ページング・画像選択はその上に置く。
5. **マルチターゲット。** `net9.0` = Jellyfin 10.11 ABI、`net10.0` = Jellyfin 12.0 ABI。
6. **リリース成果物は `scripts/package.sh` が作る。** メタ情報は
   `scripts/meta.template.json` が単一の出所。
7. **UI 文言は英語。** README / docs / コミット本文は日本語でよい。

## リポジトリ構造

```
.devcontainer/             app + Jellyfin 12.0 + Jellyfin 10.11
.github/workflows/         integration.yaml, deployment.yaml
scripts/                   package.sh, manifest.py, deploy.sh, meta.template.json
Jellyfin.Plugin.Spotify/
  Plugin.cs
  PluginServiceRegistrator.cs
  Configuration/
  Catalog/
    SpotifyTokenProvider.cs  Client Credentials, token expiry, single-flight
    SpotifyTransport.cs      Web API HTTP, 401 / 403 / 404 / 429
    Caching/                 byte-budget LRU, disk cache, in-flight coalescing
    Throttling/              serialisation, interval, cooldown
    ICatalogTransport.cs     raw JSON contract
    ISpotifyCatalog.cs       track / album / artist lookup contract
    SpotifyCatalog.cs        official DTO parsing, search, ID lookup, paging
    Models/                  transport-independent catalog models
    Official/                Spotify Web API response DTOs
    ArtworkSelector.cs       nearest fixed-size image selection
  ExternalIds/              3 IExternalId + open.spotify.com links
tests/Jellyfin.Plugin.Spotify.Tests/
```

## 引き継いだ設計判断

**`Catalog/` は `MediaBrowser.*` を参照しない。** ロジックはここに寄せ、サーバー無しの
ユニットテストで検証する。

**`ICatalogTransport` は生 JSON を返す。** キャッシュがレスポンスをそのまま保存でき、
シリアライズの往復が要らない。

**429 は `null` ではなく例外。** `null` は not found としてキャッシュされるため。
Spotify は `Retry-After` を返すので、Apple 版の推測 cooldown よりそちらを優先する。

**構成は cache → throttle → network。** キャッシュヒットは pace しない。実通信は
すべて直列化し、同一 URL の同時リクエストは 1 本に束ねる。

**メモリ上限は件数でなくバイト。** 応答サイズが endpoint で大きく違うため。大きい
応答はディスクに書かない（既定 8 KB 超）。ディスクは 1 entry 1 file、SHA-256 の
先頭 2 文字で shard する。SQLite は Jellyfin の native dependency と衝突する恐れが
あるため使わない。

**アートワークは固定 URL の配列。** URL template ではない。`ArtworkSelector` が
設定サイズに最も近い image を選ぶ。音声ファイルには埋め込まない。

**Web Player の検索経路は非公開仕様。** 2026-09-11 の静的解析結果は
[docs/research/webplay-search.md](docs/research/webplay-search.md) に記録した。persisted-query
hash、TOTP、schema、header、client version は変更され得る。巨大 bundle や secret の
実値は保存しない。

## ビルド・テスト

```bash
dotnet build
dotnet build -f net10.0
dotnet test
dotnet format --verify-no-changes
./scripts/deploy.sh [--legacy]
./scripts/package.sh [version]
```

- テストには .NET 9 / 10 の両ランタイム（ASP.NET Core 含む）が要る。
- `global.json` で Microsoft.Testing.Platform に opt-in。テストは xunit v3。
- 本体の Jellyfin 参照は `ExcludeAssets=runtime`、テストは通常参照。

## コーディング規約

- `TreatWarningsAsErrors=true`。警告を残さない。
- public メンバーに XML ドキュメントコメントを書く。
- コード内のコメントと識別子は英語。
- ログは構造化ログ。補間して渡さない。
- ABI 差分は `#if JELLYFIN_10_11` / `#if JELLYFIN_12_0` で薄い層に閉じ込める。

## ブランチとリリース

`feature/*` → PR → `develop` → merge → `master` → `v*` tag。
workflow は `integration.yaml` と `deployment.yaml` の 2 本。manifest は ABI・channel
ごとに分ける。

## コミット

Conventional Commits。header 128 文字まで。type は
`build/ui/ci/docs/feat/fix/perf/refactor/revert/format/test/chore`。

## やらないこと

- Spotify の Client ID / secret、access token、利用者の credential をコミットしない。
- 利用者のログインを要求しない。公開カタログに Client Credentials で届かない機能は、
  人間に相談してから authorization code flow を検討する。
- HTML スクレイピングをしない。公式 Web API を使う。
- Jellyfin runtime assemblies、`dist/`, `bin/`, `obj/`, `media/` をコミットしない。

[apple]: https://github.com/tkgstrator/Jellyfin-AppleMusic-Metadata
