using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Web.Components
{
    public static class DialogServiceExtensions
    {
        public static async Task<bool?> ShowCustomConfirmDialogAsync(this IDialogService dialogService, string title, string contentText, string yesText = "Yes", string cancelText = "No")
        {
            var parameters = new DialogParameters
            {
                { "Title", title },
                { "ContentText", contentText },
                { "YesText", yesText },
                { "CancelText", cancelText }
            };

            var options = new DialogOptions { CloseButton = false, MaxWidth = MaxWidth.Small, FullWidth = true };
            var dialog = await dialogService.ShowAsync<Pages.SharedConfirmDialog>("", parameters, options);
            var result = await dialog.Result;
            
            if (result.Canceled)
            {
                return null;
            }
            
            return true;
        }
    }
}
