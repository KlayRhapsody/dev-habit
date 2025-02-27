# MJ Tech Pragmatic REST APIs - Dev Habit

### **在 .http 中遇到變數不支援 . 符號的問題**

```shell
@DevHabit.Api_HostAddress = http://localhost:5000

GET {{DevHabit.Api_HostAddress}}/weatherforecast/
Accept: application/json
```

改為

```shell
@DevHabit_Api_HostAddress = http://localhost:5000

GET {{DevHabit_Api_HostAddress}}/weatherforecast/
Accept: application/json
```


### **在 VS Code 中產生 Dockerfile** 

無法像 Visual Studio 透過 Docker Support 按鈕建立 Container Scaffolding Options 內容

只能透過 docker: add docker files to workspace 指令產生 Dockerfile，並且在做內容調整


### **調整 Directory 相關檔案路徑**

在 .sln 檔案中加入 src 目錄設定，使得該目錄下的 `Directory.Build.props`、`Directory.Packages.props`、.`editorconfig` 等檔案可以被讀取

```sln
Project("{xxxx-xx-xx-xxx-xxxxx}") = "src", "src", "{xxxx-xx-xx-xxx-xxxxx}"
```

Visual Studio 下的專案設定

```sln
Project("{xxxx-xx-xx-xxx-xxxxx}") = "Solution Items", "Solution Items", "{xxxx-xx-xx-xxx-xxxxx}"
	ProjectSection(SolutionItems) = preProject
		.editorconfig = "src/.editorconfig"
		Directory.Build.props = "src/.Directory.Build.props"
		Directory.Packages.props = "src/.Directory.Packages.props"
	EndProjectSection
EndProject
```

### **憑證問題**

在開發環境中，使用容器啟動服務時遇到容器中讀取不到本地的開發者憑證，設定環境變數確保容器內可以正確讀取本地掛載憑證

```shell
# 設定憑證路徑
ASPNETCORE_Kestrel__Certificates__Default__Path=/home/app/.aspnet/https/aspnetcore.pfx
# 設定憑證密碼
ASPNETCORE_Kestrel__Certificates__Default__Password=123456
```

密碼可設定於 .env 檔案中避免明碼寫入設定檔

### **Http 轉導 HTTPS 注意事項**

程式中使用 UseHttpsRedirection 進行 Https 重新導向，需確認轉導的 Port 是否正確

若未明確指定，則會讀取到環境變數所指定的 Port 導致轉導路徑錯誤