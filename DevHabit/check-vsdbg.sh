#!/bin/bash
# 確認偵錯工具是否正確安裝
echo "檢查容器中的偵錯工具..."
docker exec -it devhabit-devhabit.api-1 ls -la /vsdbg || echo "偵錯工具不存在！"
echo "檢查偵錯器執行權限..."
docker exec -it devhabit-devhabit.api-1 ls -la /vsdbg/vsdbg || echo "偵錯器執行檔不存在！"
echo "檢查容器是否具有正確的執行權限..."
docker exec -it devhabit-devhabit.api-1 file /vsdbg/vsdbg || echo "無法檢查檔案類型！"
