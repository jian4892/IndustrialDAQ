using System.Collections.ObjectModel;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Core.ResourceTree;
using IndustrialDAQ.UI.Events;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace IndustrialDAQ.UI.ViewModels;

public class ResourceRuleConfigViewModel : BindableBase, INavigationAware
{
    private readonly IAlarmDefinitionRepository _repository;
    private readonly IAlarmDefinitionService _alarmDefinitionService;
    private readonly Prism.Events.IEventAggregator _eventAggregator;

    public ObservableCollection<AlarmDefinition> Rules { get; } = new();

    private AlarmDefinition? _selectedRule;
    public AlarmDefinition? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (SetProperty(ref _selectedRule, value))
            {
                RaisePropertyChanged(nameof(HasSelectedRule));
                if (value != null)
                {
                    IsEditMode = true;
                    // Clone to edit model
                    EditModel = new AlarmDefinitionEditModel
                    {
                        Id = value.Id,
                        RuleId = value.RuleId,
                        AlarmCode = value.AlarmCode,
                        ResourcePath = value.ResourcePath?.Value,
                        TargetResourcePath = value.TargetResourcePath?.Value,
                        TagId = value.TagId,
                        ConditionExpression = value.ConditionExpression,
                        Severity = value.Severity,
                        MessageTemplate = value.MessageTemplate,
                        IsEnabled = value.IsEnabled
                    };
                }
                else
                {
                    EditModel = new AlarmDefinitionEditModel();
                    IsEditMode = false;
                }
            }
        }
    }

    public bool HasSelectedRule => SelectedRule != null || !IsEditMode;

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    private AlarmDefinitionEditModel _editModel = new();
    public AlarmDefinitionEditModel EditModel
    {
        get => _editModel;
        set => SetProperty(ref _editModel, value);
    }

    public IEnumerable<AlarmSeverity> Severities => Enum.GetValues<AlarmSeverity>();

    public DelegateCommand CreateNewCommand { get; }
    public DelegateCommand SaveCommand { get; }
    public DelegateCommand DeleteCommand { get; }
    public DelegateCommand ReloadEngineCommand { get; }

    public ResourceRuleConfigViewModel(
        IAlarmDefinitionRepository repository,
        IAlarmDefinitionService alarmDefinitionService,
        Prism.Events.IEventAggregator eventAggregator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _alarmDefinitionService = alarmDefinitionService ?? throw new ArgumentNullException(nameof(alarmDefinitionService));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

        CreateNewCommand = new DelegateCommand(OnCreateNew);
        SaveCommand = new DelegateCommand(OnSaveExecute);
        DeleteCommand = new DelegateCommand(OnDeleteExecute);
        ReloadEngineCommand = new DelegateCommand(OnReloadEngineExecute);
    }

    private void OnCreateNew()
    {
        SelectedRule = null;
        IsEditMode = false;
        EditModel = new AlarmDefinitionEditModel
        {
            RuleId = "new-rule-" + Guid.NewGuid().ToString("N")[..6],
            AlarmCode = "ALARM_CODE",
            Severity = AlarmSeverity.Warning,
            IsEnabled = true
        };
        RaisePropertyChanged(nameof(HasSelectedRule));
    }

    private async void OnSaveExecute()
    {
        if (string.IsNullOrWhiteSpace(EditModel.RuleId) || string.IsNullOrWhiteSpace(EditModel.AlarmCode))
        {
            _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
            {
                Title = "验证失败", Message = "RuleId 和 AlarmCode 不能为空。", Type = NotificationType.Warning
            });
            return;
        }

        try
        {
            var def = new AlarmDefinition
            {
                Id = IsEditMode ? EditModel.Id : Guid.NewGuid().ToString("N"),
                RuleId = EditModel.RuleId,
                AlarmCode = EditModel.AlarmCode,
                ResourcePath = string.IsNullOrWhiteSpace(EditModel.ResourcePath) ? null : ResourcePath.Parse(EditModel.ResourcePath),
                TargetResourcePath = string.IsNullOrWhiteSpace(EditModel.TargetResourcePath) ? null : ResourcePath.Parse(EditModel.TargetResourcePath),
                TagId = EditModel.TagId ?? string.Empty,
                ConditionExpression = EditModel.ConditionExpression ?? string.Empty,
                Severity = EditModel.Severity,
                MessageTemplate = EditModel.MessageTemplate ?? string.Empty,
                IsEnabled = EditModel.IsEnabled,
                Enabled = EditModel.IsEnabled, // Sync legacy property
                AckPolicy = AlarmAckPolicy.Required,
                ClearPolicy = AlarmClearPolicy.AutoClearWhenConditionFalse
            };

            await _repository.UpsertAsync(def);
            
            _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
            {
                Title = "保存成功", Message = $"规则 {def.RuleId} 已保存。请点击应用并热重载以生效。", Type = NotificationType.Success
            });

            await LoadRulesAsync();
            SelectedRule = Rules.FirstOrDefault(r => r.RuleId == def.RuleId);
        }
        catch (Exception ex)
        {
            _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
            {
                Title = "保存失败", Message = ex.Message, Type = NotificationType.Error
            });
        }
    }

    private async void OnDeleteExecute()
    {
        if (IsEditMode && !string.IsNullOrWhiteSpace(EditModel.RuleId))
        {
            try
            {
                await _repository.DisableAsync(EditModel.RuleId);
                _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
                {
                    Title = "删除成功", Message = $"规则 {EditModel.RuleId} 已禁用/删除。", Type = NotificationType.Success
                });
                await LoadRulesAsync();
                SelectedRule = null;
            }
            catch (Exception ex)
            {
                _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
                {
                    Title = "删除失败", Message = ex.Message, Type = NotificationType.Error
                });
            }
        }
    }

    private async void OnReloadEngineExecute()
    {
        try
        {
            await _alarmDefinitionService.ReloadAsync();
            _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
            {
                Title = "热重载成功", Message = "底层报警规则引擎已重新编译并生效。", Type = NotificationType.Success
            });
        }
        catch (Exception ex)
        {
            _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
            {
                Title = "热重载失败", Message = ex.Message, Type = NotificationType.Error
            });
        }
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = LoadRulesAsync();
    }

    private async Task LoadRulesAsync()
    {
        var rules = await _repository.LoadAllAsync();
        
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Rules.Clear();
            foreach (var r in rules)
            {
                Rules.Add(r);
            }
        });
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }
}

public class AlarmDefinitionEditModel : BindableBase
{
    public string Id { get; set; } = string.Empty;
    
    private string _ruleId = string.Empty;
    public string RuleId { get => _ruleId; set => SetProperty(ref _ruleId, value); }

    private string _alarmCode = string.Empty;
    public string AlarmCode { get => _alarmCode; set => SetProperty(ref _alarmCode, value); }

    private string? _resourcePath;
    public string? ResourcePath { get => _resourcePath; set => SetProperty(ref _resourcePath, value); }
    
    private string? _targetResourcePath;
    public string? TargetResourcePath { get => _targetResourcePath; set => SetProperty(ref _targetResourcePath, value); }

    private string? _tagId;
    public string? TagId { get => _tagId; set => SetProperty(ref _tagId, value); }

    private string? _conditionExpression;
    public string? ConditionExpression { get => _conditionExpression; set => SetProperty(ref _conditionExpression, value); }

    private AlarmSeverity _severity;
    public AlarmSeverity Severity { get => _severity; set => SetProperty(ref _severity, value); }

    private string? _messageTemplate;
    public string? MessageTemplate { get => _messageTemplate; set => SetProperty(ref _messageTemplate, value); }

    private bool _isEnabled = true;
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
}
