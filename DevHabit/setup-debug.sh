#!/bin/bash
# 設定 Docker 遠端偵錯環境

echo "停止現有容器..."
docker compose -f ./docker-compose.debug.yml down

echo "重新建置 API 映像檔，不使用快取..."
docker compose -f ./docker-compose.debug.yml build --no-cache devhabit.api

echo "啟動容器..."
docker compose -f ./docker-compose.debug.yml up -d

echo "等待容器完全啟動..."
sleep 5

echo "檢查容器中的偵錯工具..."
docker exec -it devhabit-devhabit.api-1 ls -la /vsdbg || echo "偵錯工具不存在！"

echo "檢查偵錯器執行權限..."
docker exec -it devhabit-devhabit.api-1 ls -la /vsdbg/vsdbg || echo "偵錯器執行檔不存在！"

echo "如果需要，在容器中手動安裝偵錯工具..."
docker exec -it devhabit-devhabit.api-1 bash -c '
if [ ! -f /vsdbg/vsdbg ]; then
    echo "偵錯工具不存在，正在安裝..."
    apt-get update
    apt-get install -y --no-install-recommends unzip curl procps
    curl -sSL https://aka.ms/getvsdbgsh | /bin/bash /dev/stdin -v latest -l /vsdbg
    chmod +x /vsdbg/vsdbg
    echo "安裝完成！"
fi
'

echo "偵錯環境設定完成。"
echo "現在，您可以在 VS Code 中使用 'Docker Compose: Debug' 設定來啟動偵錯。"
