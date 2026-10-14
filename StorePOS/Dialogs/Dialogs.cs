using System.Windows;
using StorePOS.Models;

namespace StorePOS.Dialogs;

/// <summary>Opens the modal dialogs, centred on the window that is in front.</summary>
public static class Dialogs
{
    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;

    private static bool Show(Window dialog)
    {
        var owner = Owner;
        if (owner != null && owner != dialog && owner.IsVisible)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        return dialog.ShowDialog() == true;
    }

    /// <summary>Destructive actions. The safe choice is first and focused.</summary>
    public static bool Confirm(string title, string message, string keepText, string actionText) =>
        Show(new ConfirmDialog(title, message, keepText, actionText));

    /// <summary>Void a sale or reverse a repayment: a summary plus a required reason. Returns null if cancelled.</summary>
    public static string? AskReason(string title, IReadOnlyList<(string Label, string Value)> summary,
        string paragraph, string placeholder, string actionText)
    {
        var dialog = new ReasonDialog(title, summary, paragraph, placeholder, actionText);
        return Show(dialog) ? dialog.Reason : null;
    }

    public static void Error(string message) => Show(new MessageDialog(Infrastructure.Loc.Get("Error_Title"), message));

    public static void Info(string title, string message) => Show(new MessageDialog(title, message));

    /// <summary>New (null) or edit. Returns the saved customer's id.</summary>
    public static long? EditCustomer(Customer? customer)
    {
        var dialog = new CustomerDialog(customer);
        return Show(dialog) ? dialog.SavedId : null;
    }

    /// <summary>Add (null) or edit. Returns the saved product's id.</summary>
    public static long? EditProduct(Product? product)
    {
        var dialog = new ProductDialog(product);
        return Show(dialog) ? dialog.SavedId : null;
    }

    public static bool ImportCsv() => Show(new ImportCsvDialog());

    /// <summary>PR9: read-only, so there is nothing to return.</summary>
    public static void PriceHistory(Product product) => Show(new PriceHistoryDialog(product));

    public static bool RecordRepayment(CustomerBalance customer) => Show(new RepaymentDialog(customer));

    /// <summary>ST3. Returns the new stock-in's id.</summary>
    public static long? RecordStockIn()
    {
        var dialog = new RecordStockInDialog();
        return Show(dialog) ? dialog.SavedId : null;
    }

    /// <summary>ST6, starting on the given product if any. Returns the adjusted product's id.</summary>
    public static long? AdjustStock(long? productId)
    {
        var dialog = new AdjustStockDialog(productId);
        return Show(dialog) ? dialog.SavedProductId : null;
    }
}
