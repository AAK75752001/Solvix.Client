using Solvix.Client.MVVM.ViewModels;

namespace Solvix.Client.MVVM.Views;

public partial class ChatPage : ContentPage
{
    public ChatPage(ChatPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}