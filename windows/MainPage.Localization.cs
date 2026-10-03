using Microsoft.UI.Xaml.Controls;
namespace Tomatotodo_Windows;
public sealed partial class MainPage {
    private void ApplyXamlLanguage() {
        ToolTipService.SetToolTip(ShellBackButton, Data.UiText.T("返回"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ShellBackButton, Data.UiText.T("返回"));
        ToolTipService.SetToolTip(ShellPaneButton, Data.UiText.T("展开或收起导航"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ShellPaneButton, Data.UiText.T("展开或收起导航"));
        ShellSearchBox.PlaceholderText = Data.UiText.T("搜索 Tomatotodo");
        ToolTipService.SetToolTip(ShellSearchButton, Data.UiText.T("搜索"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ShellSearchButton, Data.UiText.T("搜索"));
        LocalizedElement0.Content = Data.UiText.T("仪表盘");
        LocalizedElement1.Content = Data.UiText.T("配置");
        LocalizedElement2.Content = Data.UiText.T("档案");
        CourseNavigationItem.Content = Data.UiText.T("课程表");
        LocalizedElement3.Content = Data.UiText.T("工具");
        ToolTipService.SetToolTip(AccountEntryButton, Data.UiText.T("账户与个人资料"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AccountEntryButton, Data.UiText.T("账户与个人资料"));
        LocalizedElement4.Content = Data.UiText.T("常规");
        ToolTipService.SetToolTip(LocalizedElement5, Data.UiText.T("返回常规"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LocalizedElement5, Data.UiText.T("返回常规"));
        LocalizedElement6.Text = Data.UiText.T("常规");
        ToolTipService.SetToolTip(DashboardEditButton, Data.UiText.T("编辑仪表盘"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DashboardEditButton, Data.UiText.T("编辑仪表盘"));
        ToolTipService.SetToolTip(DashboardAddButton, Data.UiText.T("添加组件"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DashboardAddButton, Data.UiText.T("添加组件"));
        ToolTipService.SetToolTip(DashboardSaveButton, Data.UiText.T("保存布局"));
        LocalizedElement7.Text = Data.UiText.T("保存布局");
        ToolTipService.SetToolTip(LocalizedElement8, Data.UiText.T("历史任务"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LocalizedElement8, Data.UiText.T("历史任务"));
        ToolTipService.SetToolTip(LocalizedElement9, Data.UiText.T("配置排序"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LocalizedElement9, Data.UiText.T("配置排序"));
        ToolTipService.SetToolTip(ArchiveExportButton, Data.UiText.T("保存专注明信片为 PNG"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ArchiveExportButton, Data.UiText.T("保存专注明信片为 PNG"));
        CourseSearchBox.PlaceholderText = Data.UiText.T("搜索课程、教室或教师");
        LocalizedElement10.Text = Data.UiText.T("当前周次");
        ToolTipService.SetToolTip(DashboardResetButton, Data.UiText.T("重置计时器"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DashboardResetButton, Data.UiText.T("重置计时器"));
        LocalizedElement11.Text = Data.UiText.T("添加预设");
        ToolTipService.SetToolTip(LocalizedElement12, Data.UiText.T("关闭"));
        LocalizedElement13.Text = Data.UiText.T("仪表盘");
        LocalizedElement14.Text = Data.UiText.T("添加组件");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LocalizedElement15, Data.UiText.T("关闭组件库"));
        ToolTipService.SetToolTip(LocalizedElement15, Data.UiText.T("关闭"));
        LocalizedElement16.Text = Data.UiText.T("选择组件加入仪表盘。长按磁贴可调整顺序，完成后点击“保存布局”。");
    }
}
