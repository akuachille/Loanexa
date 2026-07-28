import re

path = r'C:\Users\HP\Documents\DigitalLoanPlatform2\Web\Components\Pages\Reports.razor'

with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Replace the IncomeStatement block with the component
pattern_income = re.compile(r'    else if \(_activeReport == "IncomeStatement"\)\s*\{\s*<MudPaper Class="pa-4 mb-4".*?    \}\s*    else if \(_activeReport == "LoanProductTracker"\)', re.DOTALL)
replacement_income = '''    else if (_activeReport == "IncomeStatement")
    {
        <DigitalLoanPlatform2.Web.Components.Pages.ReportsFolder.IncomeStatementReport 
            IncomeStatement="_incomeStatement"
            IncomeStatementRange="_incomeStatementRange"
            IncomeStatementRangeChanged="(r => _incomeStatementRange = r)"
            OnGenerateReport="LoadIncomeStatement"
            OnViewDetails="(type => OpenStatementDetailsDialog(type))" />
    }
    else if (_activeReport == "LoanProductTracker")'''

content = pattern_income.sub(replacement_income, content)

# 2. Add the OpenStatementDetailsDialog function logic
pattern_method = re.compile(r'    private bool _showIncomeStatementDetails = false;')
replacement_method = '''    private async Task OpenStatementDetailsDialog(string type)
    {
        if (_incomeStatement == null) return;
        var parameters = new DialogParameters
        {
            ["Statement"] = _incomeStatement,
            ["Type"] = type
        };
        var title = type == "Income" ? "Income Details" : "Expenses & Deductions Details";
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };
        await DialogService.ShowAsync<DigitalLoanPlatform2.Web.Components.Pages.IncomeStatementDetailDialog>(title, parameters, options);
    }'''

content = pattern_method.sub(replacement_method, content)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Done.")
