# Code Conventions

## 命名规范

- **命名空间**: PascalCase（如 `OrionDesk.UI.Windows`）
- **类名**: PascalCase（如 `ClockWidget`、`WidgetManager`）
- **方法名**: PascalCase（如 `UpdateClock`、`LoadSettings`）
- **私有字段**: _camelCase（如 `_timer`、 `_settings`）
- **局部变量**: camelCase（如 `cpuUsage`、`memPercentage`）
- **常量**: PascalCase（如 `SHGFI_ICON`）

## 目录结构

```
src/
├── OrionDesk.UI/           # 表现层
│   ├── Windows/            # 窗口类
│   ├── Controls/           # 自定义控件
│   ├── Converters/         # 值转换器
│   └── Resources/          # 资源文件
├── OrionDesk.BLL/          # 业务逻辑层
│   ├── Models/             # 数据模型
│   └── Services/           # 业务服务
└── OrionDesk.DAL/          # 数据访问层
```

## 代码风格

- 使用 `var` 声明变量（类型明显时）
- 使用 `async/await` 处理异步操作
- 使用 `#region` 组织代码块
- 使用 XML 注释文档化公共 API
- 使用 `try-catch` 处理异常，记录调试信息

## WPF 规范

- 使用 MVVM 模式（当前使用代码后置，后续可重构）
- XAML 中使用 StaticResource 引用样式
- 使用 Style 统一控件外观
- 使用 Storyboard 实现动画效果

## 注释规范

```csharp
/// <summary>
/// 方法说明
/// </summary>
/// <param name="参数名">参数说明</param>
/// <returns>返回值说明</returns>
```
