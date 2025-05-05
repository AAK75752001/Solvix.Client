using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solvix.Client.Core.Converters
{
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            if (value is string str && int.TryParse(str, out int strCount))
            {
                return strCount > 0;
            }
            if (value is IEnumerable<ValidationResult> errors)
            {
                return errors.Any();
            }
            if (value is ICollection<string> stringCollection) 
            {
                return stringCollection.Count > 0;
            }


            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
