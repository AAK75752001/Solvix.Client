using Microsoft.Maui.Controls;
using Solvix.Client.MVVM.ViewModels;
using Solvix.Client.Core.Models;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace Solvix.Client.MVVM.Views
{
    public partial class ChatPage : ContentPage
    {
        private ChatPageViewModel? _viewModel => BindingContext as ChatPageViewModel;
        private bool _shouldScrollToBottom = true; // فلگ برای کنترل اسکرول

        public ChatPage(ChatPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            }

            // اضافه کردن event handler برای تشخیص اسکرول کاربر
            MessagesCollectionView.Scrolled += MessagesCollectionView_Scrolled;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // فقط اگر پیام جدیدی اضافه شده و کاربر پایین صفحه هست، اسکرول کن
            if (e.Action == NotifyCollectionChangedAction.Add && _shouldScrollToBottom)
            {
                ScrollToLastMessage(true);
            }
            // اگر لیست کامل ریست شد، حتما اسکرول کن
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _shouldScrollToBottom = true; // بعد از ریست فرض کن پایین هستیم
                ScrollToLastMessage(false); // اسکرول بدون انیمیشن
            }
        }

        // وقتی کاربر خودش اسکرول می‌کنه
        private void MessagesCollectionView_Scrolled(object? sender, ItemsViewScrolledEventArgs e)
        {
            // اگر کاربر به نزدیکی انتهای لیست اسکرول کرد، فلگ رو true کن
            // در غیر این صورت false کن تا اسکرول خودکار انجام نشه
            // این محاسبات ممکنه نیاز به تنظیم دقیق‌تر داشته باشن
            if (e.VerticalDelta > 0 && e.VerticalOffset < 50) // اگر کمی به بالا اسکرول کرد
            {
                _shouldScrollToBottom = false;
            }
            else if (e.VerticalDelta < 0 && (_viewModel?.Messages.Count ?? 0) > 0) // اگر به پایین اسکرول کرد
            {
                // چک کن آیا به آخر رسیده (با کمی تلورانس)
                // این بخش نیاز به محاسبه دقیق‌تر بر اساس ارتفاع آیتم‌ها دارد
                // فعلا اگر به پایین اسکرول کرد، فرض می‌کنیم می‌خواد پایین بمونه
                _shouldScrollToBottom = true;
            }
        }


        private void ScrollToLastMessage(bool animate = true)
        {
            if (_viewModel != null && _viewModel.Messages.Any())
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Task.Delay(50).ContinueWith(_ =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var lastMessage = _viewModel.Messages.LastOrDefault();
                            if (lastMessage != null && MessagesCollectionView != null)
                            {
                                MessagesCollectionView.ScrollTo(lastMessage, position: ScrollToPosition.End, animate: animate);
                            }
                        });
                    });
                });
            }
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

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _shouldScrollToBottom = true;
            ScrollToLastMessage(false); 
        }
    }
}
