# WagahighChoices 2nd VISION
ワガママハイスペックの全ルートを探索して、誰の初期好感度が最も高いかを調べよう！

途中経過報告

1. https://1drv.ms/p/s!Am67Z1cgKi98g9Ia-1lCD5eDsUPrSQ
2. https://1drv.ms/p/s!Am67Z1cgKi98g9k_5Ac-y07q3_LLUg

# 今回こそは！
ワガママハイスペックを Docker に乗せて分散処理させて、 Windows マシンを占有せずに探索します。

![構成図](https://i.gyazo.com/9c7627d6bc8b1cb30318b059e4d6d117.png)

# プロジェクト構成
## Kaoruko
Ashe を制御する中央サーバーです。

Ashe との通信用の gRPC サーバーと、管理用の Web サーバーが含まれる予定です。

依存: Ashe.Contract

## Ashe
Kaoruko からの命令を受け、探索を行います。ワガママハイスペックを操作するために Toa を利用します。

Windows からデバッグするときには、 gRPC 経由で Toa.Standalone と通信します。コンテナ上では Toa.Core を使用して直接ワガママハイスペックを操作します。

依存: Toa.Core, Toa.Grpc, Blockhash

## Ashe.Contract
Kaoruko と Ashe の通信に使用する MagicOnion のサービスがここに置かれます。

## Toa.Core
ワガママハイスペックのプロセスの管理と X Window System を経由したウィンドウの操作、およびログファイルの読み取りを行います。

## Toa.Standalone
gRPC サーバーとして Toa の機能を公開します。これは Mihiro と Ashe のデバッグ時に利用されます。

依存: Toa.Grpc

## Toa.Grpc
Toa.Standalone へアクセスするクライアントライブラリと、 MagicOnion サービスおよび gRPC サーバーのラッパーが含まれています。

依存: Toa.Core

## Mihiro
Toa.Standalone から得られる情報をプレビューしたり、 Toa 経由でゲームを操作するテストを行うためのツールです。 Ashe を開発するためのパラメータを得るために利用されます。

このプロジェクトは WPF アプリケーションなので、 .NET Core では動きません。

依存: Toa.Grpc, Blockhash

## Blockhash
[Blockhash](http://blockhash.io/) の C# 実装です。スクリーンショットの比較に利用されます。
