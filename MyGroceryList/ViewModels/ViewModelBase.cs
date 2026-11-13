using CommunityToolkit.Mvvm.ComponentModel;
using MyGroceryList.Domain.Shopping;
using MyGroceryList.Mobile.Services;

namespace MyGroceryList.Mobile.ViewModels;

public abstract partial class ViewModelBase(IUserInteraction interaction) : ObservableObject
{
    protected IUserInteraction Interaction { get; } = interaction;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    protected async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = "";
        try { await operation(); }
        catch (ShoppingValidationException exception) { ErrorMessage = exception.Message; }
        catch (OperationCanceledException) { ErrorMessage = "Operação cancelada. Tente novamente."; }
        catch (Exception exception)
        {
            Interaction.ReportFailure(exception);
            ErrorMessage = "Não foi possível salvar ou carregar os dados. Tente novamente. Seus dados salvos foram preservados.";
        }
        finally { IsBusy = false; }
    }
}
