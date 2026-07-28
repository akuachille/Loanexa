using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string path = @"C:\Users\HP\Documents\DigitalLoanPlatform2\Web\Components\Pages\Reports.razor";
        string content = File.ReadAllText(path);

        // Replace the IncomeStatement block with the component
        string patternIncome = @"(?s)    else if \(_activeReport == ""IncomeStatement""\)\s*\{\s*<MudPaper Class=""pa-4 mb-4"".*?    \}\s*    else if \(_activeReport == ""LoanProductTracker""\)";
        string replacementIncome = @"    else if (_activeReport == ""IncomeStatement"")
    {
        <DigitalLoanPlatform2.Web.Components.Pages.ReportsFolder.IncomeStatementReport 
            IncomeStatement=""_incomeStatement""
            IncomeStatementRange=""_incomeStatementRange""
            IncomeStatementRangeChanged=""(r => _incomeStatementRange = r)""
            OnGenerateReport=""LoadIncomeStatement""
            OnViewDetails=""(type => OpenStatementDetailsDialog(type))"" />
    }
    else if (_activeReport == ""LoanProductTracker"")";

        content = Regex.Replace(content, patternIncome, replacementIncome);

        // Add the OpenStatementDetailsDialog function logic
        string patternMethod = @"(?s)    private bool _showIncomeStatementDetails = false;";
        string replacementMethod = @"    private async Task OpenStatementDetailsDialog(string type)
    {
        if (_incomeStatement == null) return;
        var parameters = new DialogParameters
        {
            [""Statement""] = _incomeStatement,
            [""Type""] = type
        };
        var title = type == ""Income"" ? ""Income Details"" : ""Expenses & Deductions Details"";
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };
        await DialogService.ShowAsync<DigitalLoanPlatform2.Web.Components.Pages.IncomeStatementDetailDialog>(title, parameters, options);
    }";
        
        content = Regex.Replace(content, patternMethod, replacementMethod);

        File.WriteAllText(path, content);
        Console.WriteLine("Done.");
    }
}
