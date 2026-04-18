# AGENTS

## C# Language Policy (Prefer Latest)

- 新規実装・リファクタでは、可読性を損なわない範囲で、より新しい C# 記法・機能を優先して採用する。
- 既存コードとの整合性と互換性を保ちつつ、段階的に新しい記法へ移行する。

### Preferred Syntax

- Null チェックは `ArgumentNullException.ThrowIfNull(...)` を優先する。
- 適用可能な箇所では `primary constructor` を使用する。
- 型推論が明確な場合は `new()` を使用する。
- 使える場面では collection expression (`[]`) を使用する。
- 条件分岐が簡潔になる場合は `switch expression` を使用する。
- パターンマッチ (`is`, `is not`, `and/or`, 再帰パターン) を積極的に使用する。
- `using` は file-scoped namespace / using declaration など簡潔な記法を優先する。
- データ表現には `required` / `init` / `record` を優先する。

### Async / Time / Logging Conventions

- 不要な `Task.Run(async () => ...)` は避け、直接 `await` を優先する。
- 時刻は `TimeProvider` + `DateTimeOffset` を基本とし、`DateTime.Now` の新規利用は避ける。
- ログは構造化ログを優先する。

### Safety / Compatibility

- 変更時は TargetFramework / LangVersion / 依存ライブラリとの整合性を確認する。
- 可読性や互換性を下げる変更は行わない。
- 変更後はビルド確認を行い、回帰がないことを確認する。

## Architecture Policy (Clean Architecture / DDD)

- 新規実装・大きな改修では、Clean Architecture と DDD の原則を優先する。
- 依存関係は内側（Domain）に向ける。外側（UI / Infrastructure）から内側へは依存してよいが、内側から外側への依存は禁止する。
- ドメイン層には業務ルールと不変条件のみを置き、永続化・UI・外部サービスの詳細を持ち込まない。
- ユースケース（Application 層）は「何をするか」を記述し、「どう保存するか」「どう通信するか」は抽象（interface）越しに扱う。
- Infrastructure 層は技術詳細（DB、API、ファイル、メッセージング等）の実装責務を持ち、ドメイン知識を持たせない。
- エンティティ・値オブジェクトは用語（ユビキタス言語）を反映した命名を行い、プリミティブ値の乱用を避ける。
- ビジネス上重要なルールはドメインモデルで表現し、Application 層や UI 側に分散させない。
- テストは Domain / Application を優先し、インフラ詳細に依存しない形で主要ユースケースの回帰を防ぐ。
- 既存構造との整合性を尊重し、一括変更ではなく段階的に適用する。

## Dependency Injection Policy

- `IServiceProvider` の直接利用や `GetService(...)` 呼び出しによる Service Locator パターンは採用しない。
- 依存はコンストラクタインジェクション（DI）で明示的に受け取る。
- やむを得ず遅延解決が必要な場合も、専用ファクトリ/抽象を介して依存関係を明示し、呼び出し側にロケータを露出させない。

## Documentation Sync Policy

- 実装変更・仕様変更・挙動変更を行った場合は、必要に応じて関連する `.md` ドキュメント（`README.md` / `docs/*.md` / 運用ドキュメント）を同一変更内で更新する。
- 少なくとも、ユーザーや開発者の判断に影響する差分（手順、設定、コマンド仕様、制約）はドキュメントへ反映する。
- ドキュメント更新が不要と判断した場合は、その理由を PR / コミットメッセージ等で明示する。
- 新規ドキュメント追加時も既存ドキュメントとの導線（相互リンク）を維持し、重複・矛盾を残さない。