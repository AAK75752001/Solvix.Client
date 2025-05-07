using Solvix.Client.MVVM.ViewModels;

namespace Solvix.Client.MVVM.Views
{
    public partial class NewChatPage : ContentPage
    {
        private readonly NewChatViewModel _viewModel;

        public NewChatPage(NewChatViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadDataAsync();
        }
    }
}