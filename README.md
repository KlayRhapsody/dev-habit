# MJ Tech Pragmatic REST APIs - Dev Habit

> 在此紀錄開發過程中遇到的問題以及如何解決與開發時注意事項

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


### **`aspire-dashboard` Image 使用注意事項**

該服務會使用到 ASP.NET Core DataProtection，若啟動容器時未持久化 DataProtection-Keys，則在下一次容器啟動後，原本開啟的瀏覽器客端會無法讀取 Trace 與 Metrics 資訊，必須重開瀏覽器 (或清除 Cookies) 才能正常讀取

```shell
Unhandled exception rendering component: The key {f93a3761-8570-4693-9eaa-cb8800397a8d} was not found in the key ring.
```


### **請求屬性驗證**

`[ApiController]` 屬性本身內建了基本的 ModelState.IsValid 驗證

當有 required 欄位卻帶 null 時，會自動回傳 400 Bad Request 且 problem detail 回應內容

```json
{
    "errors": {
        "Name": [
            "The Name field is required."
        ]
    },
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "traceId": "00-af76bc87e8240a8325d0cfadbd94e049-c73a95ed1ee8345b-01"
}
```

可以在欄位上使用簡單的 DataAnnotations 屬性來設定驗證規則，以下為複數錯誤回應內容

```json
{
    "errors": {
        "Name": [
            "The Name field is required.",
            "The field Name must be a string or array type with a minimum length of '3'."
        ]
    },
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "traceId": "00-ad2310d80c3ac6954d94f0e08e227be5-1927c9863b4c1e06-01"
}
```


### **錯誤回應方法與類型**

| **方法** | **適用情境** | **回應格式** | **是否包含 `errors`** |
|----------|-------------|--------------|-----------------|
| `Problem()` | 一般 API 錯誤 (`500`, `403`, `404`) | `ProblemDetails` | ❌ |
| `ProblemDetails` | 任何錯誤訊息的標準格式 | `ProblemDetails` | ❌ |
| `BadRequest()` | 400 錯誤（可回應 `string` 或 `ProblemDetails`） | `string` 或 `ProblemDetails` | ❌ |
| `ValidationProblem()` | Model 驗證錯誤 (`ModelState`, `FluentValidation`) | `ValidationProblemDetails` | ✅ |
| `ValidationProblemDetails` | 表單/輸入驗證錯誤 | `ValidationProblemDetails` | ✅ |



### **發生 Race Condition 問題時，回應的錯誤訊息**

從下列資訊中可以看出暴露太細節的錯誤資訊給客端，可能會導致安全性問題

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
    "title": "Microsoft.EntityFrameworkCore.DbUpdateException",
    "status": 500,
    "detail": "An error occurred while saving the entity changes. See the inner exception for details.",
    "traceId": "00-c81a43d5e3d9dfaf3a14b5cbd0001d04-b408e8bd85d59591-01",
    "requestId": "0HNARP16GEF9J:00000001",
    "exception": {
        "valueKind": 1
    }
}
```

拿掉以下註冊的服務會讓錯誤格式變回 `text/plain` 且暴露更多資訊

```csharp
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
    };
});
```

```text
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
 ---> Npgsql.PostgresException (0x80004005): 23505: duplicate key value violates unique constraint "ix_tags_name"

DETAIL: Detail redacted as it may contain sensitive data. Specify 'Include Error Detail' in the connection string to include this information.
   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
```

透過實現 IExceptionHandler 介面來自訂錯誤回應格式

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
    "title": "Internal Server Error",
    "status": 500,
    "detail": "An unhandled exception occurred while processing the request. Please try again later.",
    "traceId": "00-4738a58d211f1e2aa99a8af451427f7c-4ce0550b7605be31-01",
    "requestId": "0HNARP7LBIOFK:00000001"
}
```

### **註冊 ExceptionHandler 有順序性問題**

先註冊的會先執行

```csharp
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
```


### **自身電腦重啟後額外遇到的 8080 Port 被佔用問題**

檢查是否有其他服務佔用 8080 Port

```shell
lsof -i :8080
```

發現是 java process 且 kill -9 砍掉後會自動重啟

確定 Java 進程的詳細資訊，發現是 Zookeeper 服務啟用

```shell
ps -fp {PID}
```

列出 brew services 並停止 Zookeeper 服務

```shell
brew services list
brew services stop zookeeper
```


### **Supporting Searching and Filtering 課程**

Supporting Searching and Filtering 課程中使用到的判斷語法看起來沒有功用，search null 才會執行右邊的運算，但是 search 本身就是 null，所以右邊的運算不會執行

```csharp
search ??= search?.Trim().ToLower();

// 調整為
search = search?.Trim().ToLower();
``` 

另外以下語法會產生以下警告

```csharp
query = query.Where(h => h.Name.ToLower().Contains(search) ||
    h.Description != null && h.Description.ToLower().Contains(search));
```

警告訊息

建議採用 'StringComparison' 列舉值之 'string.Contains(string)' 的字串比較方法多載，以執行不區分大小寫的比較，但請注意，這可能會導致行為發生輕微變更，因此請務必在套用建議之後進行全面測試，或者如果不需要文化相關比較，請考慮使用 'StringComparison.OrdinalIgnoreCase'CA1862

在調整成建議的作法後

```csharp
query = query.Where(h => h.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
    h.Description != null && h.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase));
```

執行會遇到以下錯誤，看起來是 EF Core 不認得 StringComparison.CurrentCultureIgnoreCase 的問題

```
The LINQ expression 'DbSet<Habit>()
    .Where(h => h.Name.Contains(
        value: __search_0, 
        comparisonType: CurrentCultureIgnoreCase) || h.Description != null && h.Description.Contains(
        value: __search_0, 
        comparisonType: CurrentCultureIgnoreCase))' could not be translated. Additional information: Translation of method 'string.Contains' failed. If this method can be mapped to your custom function, see https://go.microsoft.com/fwlink/?linkid=2132413 for more information.
Translation of method 'string.Contains' failed. If this method can be mapped to your custom function, see https://go.microsoft.com/fwlink/?linkid=2132413 for more information. Either rewrite the query in a form that can be translated, or switch to client evaluation explicitly by inserting a call to 'AsEnumerable', 'AsAsyncEnumerable', 'ToList', or 'ToListAsync'. See https://go.microsoft.com/fwlink/?linkid=2101038 for more information.
```


### **使用 HttpContext 前請先註冊該服務**

當服務中有依賴 IHttpContextAccessor 時，需要先註冊該服務，否則會出現以下錯誤

```log
---> System.InvalidOperationException: Unable to resolve service for type 'Microsoft.AspNetCore.Http.IHttpContextAccessor' while attempting to activate 'DevHabit.Api.Services.LinkService'.
```

註冊 IHttpContextAccessor 服務

```csharp
builder.Services.AddHttpContextAccessor();
```


### **Custom Media Type**

```shell
application/vnd.dev-habit.hateoas+json
```

對應的結構說明

| 組成部分 | 說明 | 對應內容 |
|-----------|--------------------------|--------------------------------|
| **Top-level type** | 主要類型，表示這是應用程式數據 | `application` |
| **Vendor** | 使用 `vnd.` 表示這是一個 **自定義的(廠商專用)** 媒體類型 | `vnd.` |
| **Vendor ID** | 廠商識別名稱，通常用來標識開發組織或專案 | `dev-habit` |
| **Media type** | 特定的媒體類型名稱，描述這種數據的用途或格式 | `hateoas` |
| **Suffix** | 這個媒體類型的底層格式 | `+json` |


## **`[Consumes]`**
- **控制 API 可接受的請求格式（`Content-Type`）**
- 確保客戶端傳遞的資料格式符合 API 的要求
- 常見的 `Content-Type`：
  - `application/json`
  - `application/xml`
  - `multipart/form-data`（用於上傳文件）

使用範例：限制 `POST` API 只接受 JSON

```csharp
[HttpPost]
[Consumes("application/json")]
public IActionResult CreateItem([FromBody] ItemDto item)
{
    return Ok(item);
}
```


### **`[Produces]`**
- **控制 API 回應的格式（`Content-Type`）**
- 指定 API **應回應的 MIME 類型**
- 確保客戶端收到的格式符合 API 設計

使用範例：限制 API 只能回應 JSON

```csharp
[HttpGet]
[Produces("application/json")]
public IActionResult GetItem()
{
    var item = new ItemDto { Id = 1, Name = "Item1" };
    return Ok(item);
}
```


### **新增 Identity DbContext Migration**

```shell
dotnet ef migrations add Add_Identity -o Migrations/Identity -c ApplicationIdentityDbContext
```


### **讓多個 DbContext 共用相同的資料庫連線與交易**

使用兩個不同的 DbContext 可能會導致兩個獨立的資料庫交易，進而導致資料不一致的風險

該方法只在相同的 Database 中有效，如果是不同的 Physical Database，則只能使用 Distributed Transaction 來處理

```csharp
public async Task<IActionResult> RegisterAsync(RegisterUserDto registerUserDto)
{
    using IDbContextTransaction transaction = await appIdentityDbContext.Database.BeginTransactionAsync();
    appDbContext.Database.SetDbConnection(appIdentityDbContext.Database.GetDbConnection());
    await appDbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

    IdentityResult identityResult = await userManager.CreateAsync(identityUser, registerUserDto.Password);

    appDbContext.Users.Add(user);
    
    await appDbContext.SaveChangesAsync();

    await transaction.CommitAsync();

    return Ok(user.Id);
}
```


### **`DefaultAuthenticateScheme` 與 `DefaultChallengeScheme` 是什麼？**

這兩個屬性是在 **ASP.NET Core Identity 與 JWT 身份驗證** 中設定 **驗證與挑戰（Challenge）行為** 的。

| **屬性** | **作用** | **影響的行為** |
|----------|---------|--------------|
| `DefaultAuthenticateScheme` | 指定應用程式 **如何驗證 Token** | **當請求進來時，伺服器會解析 JWT** |
| `DefaultChallengeScheme` | 指定應用程式 **如何回應未驗證的請求** | **當 Token 過期或無效時，伺服器回傳 `401 Unauthorized`** |


### **為什麼要用 `RandomNumberGenerator` 產生 32 位元的隨機數作為 `Refresh Token`？**

程式碼範例

```csharp
private static string GenerateRefreshToken()
{
    byte[] randomNumber = RandomNumberGenerator.GetBytes(32);
    return Convert.ToBase64String(randomNumber);
}
```

相關方法比較

| **方法** | **熵強度** | **是否密碼學安全** | **可預測性** | **適合 Refresh Token？** |
|----------|------------|-----------------|--------------|------------------|
| `RandomNumberGenerator.GetBytes(32)` | 256-bit | ✅ 是 | ❌ 不可預測 | ✅ **最推薦** |
| `Guid.NewGuid()` | 128-bit | ❌ 不是 | ⚠️ 部分可預測 | ❌ 不推薦 |
| `Random.Next()` | 32-bit | ❌ 不是 | ✅ 容易預測 | ❌ 絕對不推薦 |

