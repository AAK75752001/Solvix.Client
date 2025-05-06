using Microsoft.Maui.Controls;
using Solvix.Client.MVVM.ViewModels;
using Solvix.Client.Core.Models;
using System.Collections.Specialized;

namespace Solvix.Client.MVVM.Views
{
    public partial class ChatPage : ContentPage
    {
        private ChatPageViewModel? _viewModel => BindingContext as ChatPageViewModel;
        private bool _shouldScrollToBottom = true;

        public ChatPage(ChatPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;

            if (_viewModel != null)
            {
                _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            }

            MessagesCollectionView.Scrolled += MessagesCollectionView_Scrolled;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            _shouldScrollToBottom = true;

            if (_viewModel?.Messages.Any() == true)
            {
                ScrollToLastMessage(false);
            }
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && _shouldScrollToBottom)
            {
                ScrollToLastMessage(true);
            }
        }

        private void MessagesCollectionView_Scrolled(object? sender, ItemsViewScrolledEventArgs e)
        {
            if (_viewModel?.Messages.Count <= 0) return;

            var totalItems = _viewModel.Messages.Count;
            var visibleItems = e.LastVisibleItemIndex - e.FirstVisibleItemIndex + 1;
            var lastVisibleIndex = e.LastVisibleItemIndex;

            _shouldScrollToBottom = lastVisibleIndex >= totalItems - 3;
        }

        private void ScrollToLastMessage(bool animate = true)
        {
            if (_viewModel == null || !_viewModel.Messages.Any()) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var lastMessage = _viewModel.Messages.LastOrDefault();
                if (lastMessage != null && MessagesCollectionView != null)
                {
                    MessagesCollectionView.ScrollTo(lastMessage, position: ScrollToPosition.End, animate: animate);
                }
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (_viewModel != null)
            {
                _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            }

            MessagesCollectionView.Scrolled -= MessagesCollectionView_Scrolled;
        }
    }
}