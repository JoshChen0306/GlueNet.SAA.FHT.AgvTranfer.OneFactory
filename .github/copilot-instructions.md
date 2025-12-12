# Documentation Standards

當我要求產生 Markdown 文件 (如 README, 規格書, API 文件) 時：

1. **預設路徑**：請假設該檔案將被放置於 `Markdown/` 或 `Specifications/` 資料夾中。
2. **檔案標頭**：請在回應的程式碼區塊 (Code Block) 上方，明確標註建議的檔案路徑與名稱 (例如：`path: Markdown/MyNewClass.md`)。
3. **連結參照**：若內容中有圖片或連結，請使用相對路徑參照到該指定資料夾。

# C# Coding Conventions

## Naming Rules (命名規則)
1. **Private Fields (私有欄位)**:
   - 必須使用 `my` 作為前綴，並採用 CamelCase (小駝峰式命名)。
   - **Do NOT** use the underscore prefix `_` (e.g., `_temp`).
   - **DO** use the `my` prefix (e.g., `myTemp`, `myCount`, `myService`).

   Example:
   ```csharp
   // Bad
   private int _count;
   private string _userName;

   // Good
   private int myCount;
   private string myUserName;
   ```

# XAML Naming Conventions

## View Naming Rules (視圖命名規則)

1. **UserControl (使用者控制項)**:
   - 建立 UserControl 時，檔案名稱必須以 `ViewControl` 結尾。
   - **DO** use the `ViewControl` suffix (e.g., `DashboardViewControl.xaml`, `SettingsViewControl.xaml`).

2. **Window (視窗)**:
   - 建立 Window 時，檔案名稱必須以 `Window` 結尾。
   - **DO** use the `Window` suffix (e.g., `MainWindow.xaml`, `ConfigWindow.xaml`).

   Example:
   ```
   // Bad
   Dashboard.xaml
   Settings.xaml
   Config.xaml

   // Good (UserControl)
   DashboardViewControl.xaml
   SettingsViewControl.xaml

   // Good (Window)
   MainWindow.xaml
   ConfigWindow.xaml
   ```