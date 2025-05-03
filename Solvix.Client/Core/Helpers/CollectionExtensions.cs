using System.Collections.ObjectModel;

namespace Solvix.Client.Core.Helpers
{
    public static class CollectionExtensions
    {
        // Atualiza um item em uma ObservableCollection sem substituí-lo completamente
        public static void UpdateItem<T>(this ObservableCollection<T> collection, T item)
        {
            if (collection == null || item == null)
                return;

            int index = collection.IndexOf(item);
            if (index >= 0)
            {
                // Armazena temporariamente o item
                T existing = collection[index];

                // Remove e reinsere no mesmo índice
                collection.RemoveAt(index);
                collection.Insert(index, existing);
            }
        }

        // Método sobrecarregado para atualizar um item com base no índice
        public static void UpdateItem<T>(this ObservableCollection<T> collection, int index)
        {
            if (collection == null || index < 0 || index >= collection.Count)
                return;

            // Armazena temporariamente o item
            T item = collection[index];

            // Remove e reinsere no mesmo índice
            collection.RemoveAt(index);
            collection.Insert(index, item);
        }
    }
}