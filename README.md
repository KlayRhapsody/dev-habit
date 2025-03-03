# MJ Tech Pragmatic REST APIs - Dev Habit

> 在此紀錄開發過程中遇到的問題與解決方案

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


### **動態方式載入 DbContext Configuration**

在 DbContext 中使用 `ApplyConfigurationsFromAssembly` 方法，動態載入 DbContext Configuration

有實作 `IEntityTypeConfiguration<TEntity>` 的 Configuration 類別，會自動被載入

```csharp
// ApplicationDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
	modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
}

// HabitsConfiguration.cs
public sealed class HabitConfigurations : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.HasKey(h => h.Id);

        // ...
    }
}
```


### **Postgres 與 Api 容器啟動順序**

在 docker-compose 中設定 postgres 與 api 服務，需確保 postgres 服務啟動完成後，api 服務才能正常啟動

```yaml
depends_on:
  - devhabit.postgres
```


### **定義 DTO 類別時，複雜型別內部的屬性未定義 nullable、required 或標註 JsonRequired 導致 under-posting 問題**

錯誤解釋：error S6964

意思是：

在 Controller Action 方法中作為輸入的值類型屬性（Value Type Property）應該是 nullable、required 或標註 [JsonRequired]，以避免 under-posting（未完整提供數據的問題）。

* 值類型（int、bool、DateTime 等） 不能為 null，但如果客戶端在 POST 或 PUT 請求中省略這些屬性，ASP.NET Core 可能會使用預設值（如 0、false、DateTime.MinValue），導致數據不完整（under-posting）。
* record 型別的 required 在 Model Binding 時 不會觸發 400 Bad Request，但 [Required] 會。
* ASP.NET Core 不會自動檢測 required 是否缺少值，但 [JsonRequired] 可以在 JSON 反序列化時觸發錯誤。

```csharp
public sealed record HabitDto
{
    public required FrequencyDto Frequency { get; init; }
    public required TargetDto Target { get; init; }
    public MilestoneDto? Milestone { get; init; }
}

public sealed record FrequencyDto
{
    public required FrequencyType Type { get; init; }
    public required int TimesPerPeriod { get; init; }
}

public sealed record TargetDto
{
    public required int Value { get; init; }
    public required string Unit { get; init; }
}

public sealed record MilestoneDto
{
    public required int Target { get; init; }
    public required int Current { get; init; }
}
```


### **調整商業邏輯放置在擴充方法的幾種方式**

1. 將 UpdateFromDto 擴充方法放置在 Habit Entity 類別中，封裝在 Domain Entity 內
2. 創建一個專門的類別來代表 Update Use Case (推薦)
    - 作者不喜歡命名為 Service，因為這樣可能會在之後放入更多不必要的程式碼，而複雜了原本簡單的 Use Case 商業操作
3. 專用垂直分片


### **在設計 Patch 操作時，需先仔細思考 Api 是否真有其必要提供 Patch 操作**

* 可以使用 JsonPath 套件來實作 Patch 操作
* 打破 RESTful API 規範的方式，使用 `PATCH /habits/{id}/archived` 的 Route 設計
    - archived 是內部屬性 

大部分的 API 設計不太會去使用到 Patch，抑或是會使用 Put 來取代 Patch 的操作

### **在 Patch 驗證時，使用 ModelState.IsValid 會通過**

在 Patch 操作中，使用 ModelState.IsValid 會通過驗證，但在 DB 操作時會出現 Not Null Violation 的錯誤

```csharp
// if (!ModelState.IsValid)
// 調整為
if (!TryValidateModel(habitDto))
```

使用 `ModelState.IsValid` 時錯誤訊息

```shell
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
 ---> Npgsql.PostgresException (0x80004005): 23502: null value in column "name" of relation "habits" violates not-null constraint
```

使用 `TryValidateModel` 時錯誤訊息

```json
// 第二種
{
    "errors": {
        "Name": [
            "The Name field is required."
        ]
    },
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "traceId": "00-06050e25fe816538ec607496488f632f-6c7c2a418e809936-01"
}
```

### **設定單一索引與複合索引語法**

```csharp
// 單一索引
builder.HasIndex(t => t.Name).IsUnique();
// 複合索引，即使目前只有一個屬性
builder.HasIndex(t => new { t.Name }).IsUnique();
```

### **透過 Cli 方式新增 Migration**

```shell
dotnet ef migrations add Add_Habits -o Migrations/Application 
dotnet ef migrations add Add_Tags -o Migrations/Application 
```

### **Create tag 邏輯可能會有 Race Condition 問題**

以下創建邏輯會產生 Race Condition 問題，但在這裡透過 Db tags table 設定 Unique Index 來規避問題

```csharp
[HttpPost]
public async Task<ActionResult<TagDto>> CreateTag(CreateTagDto createTagDto)
{
    Tag tag = createTagDto.ToEntity();

    if (await dbContext.Tags.AnyAsync(t => t.Name == tag.Name))
    {
        return Conflict($"A tag with the same name: {tag.Name} already exists.");
    }

    dbContext.Tags.Add(tag);

    await dbContext.SaveChangesAsync();

    TagDto tagDto = tag.ToDto();

    return CreatedAtAction(nameof(GetTag), new { id = tagDto.Id}, tagDto);
}
```