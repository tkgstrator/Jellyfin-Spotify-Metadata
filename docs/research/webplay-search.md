# Spotify Web Player の検索 API 静的解析

調査日: 2026-09-11

## 目的と範囲

`open.spotify.com` が公開 Web Player の検索に使用する通信を、配信された JavaScript
bundle の静的解析から確認した。これは Spotify が公開・保証している Web API の契約では
なく、Web Player の内部実装を記録したものである。

巨大な bundle 本体、TOTP secret の実値、取得した access token は保存しない。

## エンドポイントとリクエスト

検索は次の endpoint に対する POST で送られる。

```text
https://api-partner.spotify.com/pathfinder/v1/query
```

リクエストは GraphQL の persisted query であり、操作ごとに SHA-256 hash を指定する。
2026-09-11 に確認した値は次のとおり。

| 操作 | persisted-query hash |
| --- | --- |
| `searchTracks` | `59ee4a659c32e9ad894a71308207594a65ba67bb6b632b183abe97303a51fa55` |
| `searchAlbums` | `64ae1fe6df380b038c0a65a2606d3361bc270de6870b2fdc99cf0848b1efa6d3` |
| `searchArtists` | `270905851ba5c7faca81cfe053c2dbd8ceb4f156a0e0ef4b385af75ab69ffd13` |

送信時に必要な header は次のとおり。

- `Authorization: Bearer <access token>`
- `Content-Type: application/json`
- `App-Platform: WebPlayer`
- `Spotify-App-Version: <Web Player の動的な client version>`

`Spotify-App-Version` は固定値として扱わず、その時点の Web Player から得た値を使用する。

## access token の bootstrap

Web Player と同じ token bootstrap は、概ね次の順で行われる。

1. `https://open.spotify.com/api/server-time` からサーバー時刻を得る。
2. 配信 bundle 内にある TOTP の version と secret を使い、時刻に対応する TOTP を生成する。
3. `https://open.spotify.com/api/token` を呼び出して access token を得る。
4. 得た token を Pathfinder の `Authorization` header に付ける。

TOTP の version、secret、生成規則は非公開の内部実装であり、変更される可能性がある。
secret の実値はこのリポジトリに保存しない。

## レスポンスの読み取り

GraphQL response の検索結果は操作に応じて次の field に入る。

- track: `tracksV2`
- album: `albumsV2`
- artist: `artists`

ページングでは `pagingInfo.nextOffset` を次の offset として使う。項目の識別子が Spotify
URI（例: `spotify:track:...`）で返る箇所では、末尾の segment を Spotify ID として抽出する。

## 公開 entity ページによる ID 引き

2026-09-11 の実測では、ログインや cookie なしで
`https://open.spotify.com/{track|album|artist}/{id}` を取得できた。HTML 内の
`<script id="initialState" type="text/plain">` は Base64 エンコードされた JSON であり、
`entities.items["spotify:{type}:{id}"]` に対象 entity のメタデータが入る。

プラグインは表示 DOM を解析せず、この script を厳密に1つだけ抽出して期待 URI と完全一致
する entity を読む。Track、Album、Artist の既知 ID 引きはこの匿名経路で実通信を確認した。
HTML と埋込 JSON の schema は公開契約ではないため、欠落や変更は not found ではなく
protocol error として扱う。

## 安定性と実装上の注意

この経路は公開仕様ではない。少なくとも次の要素は予告なく変わり得る。

- persisted-query hash
- TOTP の version、secret、生成規則
- GraphQL の response schema と field 名
- 必須 header
- Web Player の client version と、その取得方法

したがって値を永続的な契約として扱わず、失敗時にどの段階が変化したかを切り分けられる
ようにする。静的解析を再実行するときも、巨大な bundle や secret の実値を成果物として
保存しない。検索では Web Player が使う JSON API の通信だけを対象とする。既知 ID 引きで
公開 entity ページを取得するときも、表示 DOM ではなく `initialState` の JSON だけを読む。

## プラグイン実装上の制約

設定で Web Player を明示的に選択した場合だけこの経路を使い、公式 API との自動
fallback は行わない。既知 ID 引きは公開 entity ページを匿名で取得するため token は不要。
検索の token bootstrap は、公開文書だけでは TOTP の難読化材料を安全に再現できないため、
プラグインには session provider の境界だけを用意し、既定実装は必要な anonymous access
token と動的 client version が供給されていないことを明示して失敗する。

確認済みの persisted operation は検索 3 種だけで、ID lookup 用の operation/hash は推測
しない。ID lookup には公開 entity ページを使う。検索結果と entity ページの schema は別々の
adapter で同じ canonical model に変換する。
