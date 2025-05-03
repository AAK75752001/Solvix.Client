using System.Collections.ObjectModel;

namespace Solvix.Client.Core.Helpers
{
    public static class CollectionExtensions
    {
        // به‌روزرسانی یک آیتم در ObservableCollection بدون جایگزینی کامل آن
        public static void UpdateItem<T>(this ObservableCollection<T> collection, int index)
        {
            if (collection == null || index < 0 || index >= collection.Count)
                return;

            // ذخیره موقت آیتم
            T item = collection[index];

            // حذف و اضافه مجدد در همان ایندکس برای به‌روزرسانی UI
            collection.RemoveAt(index);
            collection.Insert(index, item);
        }
    }
}