# Jellyfin Spotify Metadata

Spotify の Web API から曲・アルバム・アーティストのメタデータとアートワークを取得し、
Jellyfin に保存するプラグイン。

姉妹プラグインの [Jellyfin-AppleMusic-Metadata][apple] と同じ構造・キャッシュ・
スロットル・リリース経路を共有する。

## 現状

Spotify Web API への認証 transport まで実装済み。まだ DTO とカタログの
デシリアライズ、メタデータ / 画像プロバイダは無いため、**この段階では Jellyfin の
メタデータ取得元には現れない**。

| 層 | 状態 |
| --- | --- |
| プラグイン本体（設定画面、DI） | 動く |
| Client Credentials のトークン取得・期限前更新・401 時の再取得 | 動く |
| Web API transport（404 / 401 / 403 / 429、`Retry-After`） | 動く |
| 外部 ID（曲・アルバム・アーティスト）と open.spotify.com のリンク | 動く |
| 応答キャッシュ（バイト単位の LRU + ディスク + 同時リクエストの束ね） | 動く |
| スロットル（直列化・間隔・429 のクールダウン） | 動く |
| DTO / カタログ / メタデータ・画像プロバイダ | 未着手 |

## Spotify アプリの用意

[Spotify Developer Dashboard][dashboard] でアプリを作り、設定画面に Client ID と
Client secret を入れる。プラグインは [Client Credentials flow][client-credentials]
を使う。

- 読むのは公開カタログだけなので、リスナーのログインや OAuth コールバックは不要
- refresh token は無い。約 1 時間の access token を期限の 1 分前に取り直す
- Client ID / secret は Jellyfin のプラグイン設定だけに保存し、リポジトリや配布物には
  入れない
- Spotify の ID は世界共通。Market（既定 `JP`）は再生可能性と track relinking にだけ
  使い、ID と一緒には保存しない

## 認証・通信の構成

```
CachingCatalogTransport
  └─ ThrottledCatalogTransport
       └─ SpotifyTransport
            └─ SpotifyTokenProvider
```

- 404 は `null`（not found）
- 401 はトークンを捨てて 1 回だけ取り直す
- 403 は資格情報のエラーとして返す
- 429 は `Retry-After` を読み、キャッシュには残さずスロットルへ伝える

## 次に実装するもの

1. Spotify 公式レスポンスの DTO（track / album / artist / search / paging）
2. `SpotifyCatalog`（検索と ID 引き、album tracks のページング）
3. Album / Artist / Song の metadata provider
4. Album / Artist の image provider。Spotify は URL テンプレートではなく固定サイズの
   画像配列を返すので、設定サイズに最も近いものを選ぶ
5. ISRC と UPC を外部 ID として使うか決める

## ビルド・テスト

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
./scripts/package.sh [version]
```

`net9.0` が Jellyfin 10.11 ABI、`net10.0` が Jellyfin 12.0 ABI。リリースは ABI ごとに
別 zip。

## ライセンス

GPLv3。

[apple]: https://github.com/tkgstrator/Jellyfin-AppleMusic-Metadata
[dashboard]: https://developer.spotify.com/dashboard
[client-credentials]: https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow
