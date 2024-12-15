using System.Collections;
using System.Globalization;

namespace BearBackupUI.Helpers;

[ValueConversion(typeof(IList), typeof(bool))]
public class SingleListToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
		if (value is null) return false;

		if (value is IList list) return list.Count == 1;
		return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

[ValueConversion(typeof(IList), typeof(bool))]
public class MultiListToBooleanConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is null) return false;

		if (value is IList list) return list.Count >= 1;
		return false;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}