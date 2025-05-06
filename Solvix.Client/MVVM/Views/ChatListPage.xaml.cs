using Solvix.Client.MVVM.ViewModels;

namespace Solvix.Client.MVVM.Views;

public partial class ChatListPage : ContentPage
{
    private readonly ChatListViewModel _viewModel;

    public ChatListPage(ChatListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}