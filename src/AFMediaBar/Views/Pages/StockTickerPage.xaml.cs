// 盯盘组件自己的设置页：只呈现参数与重置意图，读写由作用域内的编辑器协调。
using System.Windows;
using System.Windows.Controls;
using AFMediaBar.Classes.Settings;
using AFMediaBar.Classes.Utils;
using AFMediaBar.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace AFMediaBar.Views.Pages;

/// <summary>呈现盯盘组件设置，编辑器由既有设置作用域拥有。</summary>
public partial class StockTickerPage : INavigableView<WidgetsSettingsViewModel>
{
    public WidgetsSettingsViewModel ViewModel { get; }

    public StockTickerPage(WidgetsSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs args) => SettingsRevealAnimator.Play(sender as Panel);

    private async void ResetButton_Click(object sender, RoutedEventArgs args)
    {
        if (await SettingsResetDialog.ConfirmAsync("Settings.Page.StockTicker", cancellationToken: ViewModel.ContextCancellationToken))
            ViewModel.ResetSection(SettingsResetScope.StockTicker);
    }
}
