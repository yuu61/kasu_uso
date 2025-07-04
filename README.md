# [カスの嘘ジェネレータ (KasuUso Generator)](https://tukushityann.net)

## 概要

「カスの嘘ジェネレータ」は、OpenAI APIを使ってカスの嘘を生成する C#/.NET アプリケーションです  
日常のちょっとした雑談やプレゼンのアクセント、SNS投稿ネタとして活用できます

[カスの嘘 - Wikipedia](https://ja.wikipedia.org/wiki/%E3%83%80%E3%82%A6%E3%83%8A%E3%83%BC%E7%B3%BB%E3%81%8A%E5%A7%89%E3%81%95%E3%82%93%E3%81%AB%E6%AF%8E%E6%97%A5%E3%82%AB%E3%82%B9%E3%81%AE%E5%98%98%E3%82%92%E6%B5%81%E3%81%97%E8%BE%BC%E3%81%BE%E3%82%8C%E3%82%8B%E9%9F%B3%E5%A3%B0)

## 主な機能

- **カスタムプロンプト対応**：ユーザーが設定したテーマやキーワードに基づきカスの嘘を生成  
- **シンプルな UI** <!--物は言いよう--> ：Blazorベースの画面で入力→生成をワンストップ  
- **API キー管理**：ローカルファイル（`API_KEY.credential`）に格納したキーを自動読み込み  
- **非同期処理**：`HttpClient` と非同期メソッドで快適なレスポンス  

## 必要環境

- [.NET 8.0 SDK](https://dotnet.microsoft.com/) 以上  
- C# 11.0  
- OpenAI API アクセス権（OpenAI API キー）  
- Windows/macOS/Linux 上のターミナルまたは Visual Studio 2022／Visual Studio Code  

## インストールとセットアップ

1. リポジトリをクローン
  ```bash
  git clone https://github.com/yuu61/kasu_uso.git
  cd kasu_uso
  ```

2. API キーの準備
   `home/user/kasu_uso/bin/Release/net8.0`に`API_KEY.credential` ファイルを作成し、OpenAI API キーを１行で記述します
  ```
  sk-**************…
  ```

3. .NETをインストール

[.NET をインストールする](https://learn.microsoft.com/ja-jp/dotnet/core/install/)

## 実行方法
リポジトリのルートで以下コマンドを実行
```bash
dotnet publish -c Release
sudo systemctl daemon-reload
sudo systemctl enable blazor-app
sudo systemctl start blazor-app
dotnet run
```

`https://localhost:5001`にブラウザでアクセスすると、UIが表示されます

## カスタマイズ

* **プロンプトの変更**  
  `Home.razor` 内の `systemPrompt`や`userPrompt`を編集することで、生成されるカスの噓の傾向を調整できます
  モデルの設定現在以下のようになっています
  gpt-4.1-miniは結構高いので変更をお勧めします
  ```
  model = "gpt-4.1-mini",
  messages,
  max_tokens = 1000,
  temperature = 1
  ```
* **UI の拡張**

  Blazor コンポーネントを追加し、複数テーマ選択や生成履歴機能などを組み込むことも可能です
