using System;
using System.Collections.Generic;
using System.Windows.Input;
using IndustrialDAQ.Core.Models;
using Prism.Commands;
using Prism.Mvvm;

namespace IndustrialDAQ.UI.ViewModels;

public class WriteTagDialogViewModel : BindableBase, IDialogAware
{
    private string _tagName = string.Empty;
    public string TagName
    {
        get => _tagName;
        set => SetProperty(ref _tagName, value);
    }

    private string _valueText = string.Empty;
    public string ValueText
    {
        get => _valueText;
        set => SetProperty(ref _valueText, value);
    }

    private bool _isNumericInput;
    public bool IsNumericInput
    {
        get => _isNumericInput;
        set => SetProperty(ref _isNumericInput, value);
    }

    private bool _isBooleanInput;
    public bool IsBooleanInput
    {
        get => _isBooleanInput;
        set => SetProperty(ref _isBooleanInput, value);
    }

    public List<string> BooleanOptions { get; } = new List<string> { "True", "False" };

    private string _selectedBooleanOption = "False";
    public string SelectedBooleanOption
    {
        get => _selectedBooleanOption;
        set => SetProperty(ref _selectedBooleanOption, value);
    }

    private TagDataType _dataType;

    public string Title => "写入数据";


    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }


    public DialogCloseListener RequestClose { get; }

    public WriteTagDialogViewModel()
    {
        ConfirmCommand = new DelegateCommand(OnConfirm);
        CancelCommand = new DelegateCommand(OnCancel);
    }

    private void OnConfirm()
    {
        string result = IsBooleanInput ? SelectedBooleanOption : ValueText;
        var parameters = new DialogParameters { { "ResultValue", result } };
        RequestClose.Invoke(parameters,ButtonResult.OK);
    }

    private void OnCancel()
    {
        RequestClose.Invoke(ButtonResult.Cancel);
    }

    public bool CanCloseDialog() => true;

    public void OnDialogClosed() { }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.TryGetValue("TagName", out string tagName))
            TagName = tagName;

        if (parameters.TryGetValue("DataType", out TagDataType dataType))
            _dataType = dataType;

        if (parameters.TryGetValue("CurrentValue", out string currentValue))
        {
            if (currentValue == "-") currentValue = "";
            ValueText = currentValue;
            SelectedBooleanOption = (currentValue.Equals("True", StringComparison.OrdinalIgnoreCase) || currentValue == "1") ? "True" : "False";
        }

        IsBooleanInput = _dataType == TagDataType.Bool;
        IsNumericInput = !IsBooleanInput;
    }
}
