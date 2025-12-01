# s4na VPM Repository

VRChat Creator Companion (VCC) 用パッケージリポジトリ

## Add to VCC

以下のURLをVCCに追加してください:

<https://s4na.github.io/vpm/index.json>

または以下のボタンから追加:

[![Add to VCC](https://img.shields.io/badge/Add%20to-VCC-blue)](https://s4na.github.io/vpm/)

## Packages

| Package | Description |
|---------|-------------|
| com.s4na.utils | Utility scripts collection |

## Setup (for maintainers)

1. GitHub Pages を有効化: Settings > Pages > Source を "GitHub Actions" に設定
2. main ブランチに push すると自動的にパッケージリストがビルドされます

## Adding a New Package

1. `Packages/com.your.package/` ディレクトリを作成
2. `package.json` を追加（VPM形式）
3. スクリプトを追加
4. main に push

## License

MIT
