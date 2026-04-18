# Release Guide

## 概要

タグ `v*` を push すると GitHub Actions が `Release` ビルドを実行し、`SR1CTRL-<tag>-win-x64.zip` を GitHub Release に自動添付します。

## 事前準備

- GitHub リポジトリで Actions を有効化する
- 既定の `GITHUB_TOKEN` が有効であることを確認する

## 使い方

1. ローカルで `Release` ビルドが通ることを確認する
2. バージョンタグを作成する
3. タグを push する

```powershell
dotnet build SR1CTRL.slnx -c Release
git tag v1.0.0
git push origin v1.0.0
```

## ワークフロー

- 定義ファイル: `.github/workflows/release.yml`
- トリガー:
  - `push` (`v*` タグ)
  - `workflow_dispatch` (手動実行)
- 実行内容:
  - `dotnet restore SR1CTRL.slnx`
  - `dotnet build SR1CTRL.slnx -c Release`
  - `dotnet publish SR1CTRL/SR1CTRL.csproj -c Release -r win-x64`
  - 出力を ZIP 化して Release に添付
