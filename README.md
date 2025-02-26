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


### **Cli 不支援新增 Solution Items 必須手動建立**

```sln
Project("{xxxx-xx-xx-xxx-xxxxx}") = "Solution Items", "Solution Items", "{xxxx-xx-xx-xxx-xxxxx}"
	ProjectSection(SolutionItems) = preProject
		.editorconfig = "src/.editorconfig"
		Directory.Build.props = "src/.Directory.Build.props"
		Directory.Packages.props = "src/.Directory.Packages.props"
	EndProjectSection
EndProject
```