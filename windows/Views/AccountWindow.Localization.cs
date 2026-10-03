using Microsoft.UI.Xaml.Controls;
namespace Tomatotodo_Windows.Views;
public sealed partial class AccountWindow {
    private void ApplyXamlLanguage() {
        LocalizedElement0.Title = Tomatotodo_Windows.Data.UiText.T("账户 · Tomatotodo");
        LocalizedElement1.Text = Tomatotodo_Windows.Data.UiText.T("账户 · Tomatotodo");
        LocalizedElement2.Text = Tomatotodo_Windows.Data.UiText.T("你的账户");
        LocalizedElement3.Text = Tomatotodo_Windows.Data.UiText.T("在独立窗口中管理账户与资料。");
        CapsLockWarning.Title = Tomatotodo_Windows.Data.UiText.T("大写锁定已开启");
        CapsLockWarning.Message = Tomatotodo_Windows.Data.UiText.T("输入密码时请注意字母大小写。");
        LocalizedElement4.Header = Tomatotodo_Windows.Data.UiText.T("本地账户");
        LocalizedElement5.Header = Tomatotodo_Windows.Data.UiText.T("云端账户");
        LocalizedElement6.Title = Tomatotodo_Windows.Data.UiText.T("云端账户");
        LocalizedElement6.Message = Tomatotodo_Windows.Data.UiText.T("连接白名单服务器。注册需要与邮箱匹配的未使用激活码；数据同步由你手动控制。");
        LocalizedElement7.Header = Tomatotodo_Windows.Data.UiText.T("邮箱");
        LocalizedElement8.Header = Tomatotodo_Windows.Data.UiText.T("常用后缀");
        LocalizedElement8.PlaceholderText = Tomatotodo_Windows.Data.UiText.T("选择后缀");
        LocalizedElement9.Header = Tomatotodo_Windows.Data.UiText.T("昵称");
        LocalizedElement9.PlaceholderText = Tomatotodo_Windows.Data.UiText.T("1–24 个字符");
        PasswordInput.Header = Tomatotodo_Windows.Data.UiText.T("密码");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LocalizedElement10, Tomatotodo_Windows.Data.UiText.T("密码要求满足程度"));
        ConfirmInput.Header = Tomatotodo_Windows.Data.UiText.T("确认密码");
        LocalizedElement11.Text = Tomatotodo_Windows.Data.UiText.T("两次输入的密码不一致");
        ActivationInput.Header = Tomatotodo_Windows.Data.UiText.T("25 位激活码");
        LocalizedElement12.Content = Tomatotodo_Windows.Data.UiText.T("记住我");
        LocalizedElement13.Content = Tomatotodo_Windows.Data.UiText.T("自动登录");
        LocalizedElement14.Text = Tomatotodo_Windows.Data.UiText.T("记住我保存邮箱；本地账户自动登录可用 30 天。云端自动登录受服务器令牌有效期限制，到期需重新登录。");
        LocalizedElement15.Content = Tomatotodo_Windows.Data.UiText.T("更换头像");
        LocalizedElement16.Text = Tomatotodo_Windows.Data.UiText.T("个人简介");
        LocalizedElement17.Content = Tomatotodo_Windows.Data.UiText.T("修改个人信息");
        LocalizedElement18.Header = Tomatotodo_Windows.Data.UiText.T("昵称");
        LocalizedElement19.Header = Tomatotodo_Windows.Data.UiText.T("个人简介");
        LocalizedElement19.PlaceholderText = Tomatotodo_Windows.Data.UiText.T("最多 300 个字符");
        LocalizedElement20.Content = Tomatotodo_Windows.Data.UiText.T("取消");
        LocalizedElement21.Content = Tomatotodo_Windows.Data.UiText.T("保存资料");
        LocalizedElement22.Text = Tomatotodo_Windows.Data.UiText.T("云端同步");
        LocalizedElement23.Text = Tomatotodo_Windows.Data.UiText.T("上传保存本机当前数据；拉取会覆盖该账户本机缓存，操作前会再次确认。");
        LocalizedElement24.Content = Tomatotodo_Windows.Data.UiText.T("从云端拉取");
        LocalizedElement25.Content = Tomatotodo_Windows.Data.UiText.T("上传到云端");
        LocalizedElement26.Text = Tomatotodo_Windows.Data.UiText.T("数据迁移");
        LocalizedElement27.Text = Tomatotodo_Windows.Data.UiText.T("先登录目标账户验证所有权，再确认覆盖。云端头像上传待对象存储配置。");
        LocalizedElement28.Header = Tomatotodo_Windows.Data.UiText.T("目标账户");
        LocalizedElement28.PlaceholderText = Tomatotodo_Windows.Data.UiText.T("选择目标账户");
        TargetPasswordInput.Header = Tomatotodo_Windows.Data.UiText.T("目标账户密码");
        LocalizedElement29.Content = Tomatotodo_Windows.Data.UiText.T("迁移并覆盖目标");
        LocalizedElement30.Content = Tomatotodo_Windows.Data.UiText.T("退出登录");
    }
}
