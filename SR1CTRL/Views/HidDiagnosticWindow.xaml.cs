using System.Collections;
using System.Text;
using System.Windows;

namespace SR1CTRL.Views;

public partial class HidDiagnosticWindow : Window
{
    public HidDiagnosticWindow()
    {
        InitializeComponent();
    }

    private void CopyDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = DiagnosticsListBox.SelectedItems;
        var source = selectedItems.Count > 0
            ? selectedItems
            : DiagnosticsListBox.Items;

        var text = JoinItems(source);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Clipboard.SetText(text);
    }

    private static string JoinItems(IList items)
    {
        var builder = new StringBuilder();
        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(item);
        }

        return builder.ToString();
    }
}
